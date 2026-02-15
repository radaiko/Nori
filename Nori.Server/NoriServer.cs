// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ NoriServer.cs
// ║║║║╬║╔╣║ Nori rendering server — integrable into any .NET host
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;

namespace Nori;

/// <summary>Nori rendering server — integrable into any .NET host</summary>
public class NoriServer {
   readonly NoriServerConfig mConfig;
   readonly ConcurrentDictionary<string, NoriSession> mSessions = new ();
   readonly SceneSerializer mSerializer = new ();
   HttpListener? mListener;

   public NoriServer (NoriServerConfig? config = null) {
      mConfig = config ?? new NoriServerConfig ();
   }

   /// <summary>The active Dwg2 scene being served</summary>
   public Dwg2? Dwg { get; set; }

   /// <summary>Number of active sessions</summary>
   public int SessionCount => mSessions.Count;

   /// <summary>Event raised when a client connects</summary>
   public event Action<NoriSession>? OnClientConnected;

   /// <summary>Event raised when a client disconnects</summary>
   public event Action<NoriSession>? OnClientDisconnected;

   /// <summary>Event raised when a client sends an interaction</summary>
   public event Action<NoriSession, InteractionMsg>? OnInteraction;

   /// <summary>Event raised when a client sends a pick request</summary>
   public event Action<NoriSession, PickMsg>? OnPick;

   // ═══════════════════════════════════════════════════════════════════════════════
   // Public API
   // ═══════════════════════════════════════════════════════════════════════════════

   /// <summary>Handle an incoming WebSocket from an external HTTP pipeline</summary>
   /// Usage with ASP.NET:
   ///   app.Map("/nori", async ctx => {
   ///      if (ctx.WebSockets.IsWebSocketRequest)
   ///         await server.HandleWebSocketAsync(await ctx.WebSockets.AcceptWebSocketAsync(), ctx.RequestAborted);
   ///   });
   public async Task HandleWebSocketAsync (WebSocket socket, CancellationToken ct = default) {
      if (mSessions.Count >= mConfig.MaxSessions) {
         await socket.CloseAsync (
            WebSocketCloseStatus.PolicyViolation,
            "Max sessions exceeded",
            ct).ConfigureAwait (false);
         return;
      }

      string id = Guid.NewGuid ().ToString ("N")[..8];
      NoriSession session = new (id, socket);
      mSessions.TryAdd (id, session);
      OnClientConnected?.Invoke (session);

      try {
         // Send current scene if available
         if (Dwg != null) {
            SceneInitMsg initMsg = mSerializer.SerializeDwg2 (Dwg);
            byte[] initBytes = mSerializer.ToBytes (initMsg);
            await session.SendAsync (initBytes, ct).ConfigureAwait (false);
         }

         // Receive loop
         byte[] buffer = new byte[64 * 1024];
         while (session.Connected && !ct.IsCancellationRequested) {
            WebSocketReceiveResult result = await socket.ReceiveAsync (
               new ArraySegment<byte> (buffer), ct).ConfigureAwait (false);

            if (result.MessageType == WebSocketMessageType.Close)
               break;

            if (result.MessageType == WebSocketMessageType.Binary) {
               byte[] data = new byte[result.Count];
               Buffer.BlockCopy (buffer, 0, data, 0, result.Count);
               DispatchClientMessage (session, data);
            }
         }
      } catch (WebSocketException) {
         // Client disconnected abruptly — handled below
      } catch (OperationCanceledException) {
         // Server shutting down — handled below
      } finally {
         mSessions.TryRemove (id, out _);
         await session.CloseAsync ().ConfigureAwait (false);
         OnClientDisconnected?.Invoke (session);
      }
   }

   /// <summary>Start a standalone HTTP+WebSocket server</summary>
   /// Usage:
   ///   await server.StartAsync("http://localhost:5100/");
   public async Task StartAsync (string url, CancellationToken ct = default) {
      mListener = new HttpListener ();
      mListener.Prefixes.Add (url);
      mListener.Start ();

      try {
         while (!ct.IsCancellationRequested) {
            HttpListenerContext ctx = await mListener.GetContextAsync ().ConfigureAwait (false);
            if (ctx.Request.IsWebSocketRequest) {
               HttpListenerWebSocketContext wsCtx =
                  await ctx.AcceptWebSocketAsync (null).ConfigureAwait (false);
               // Fire-and-forget: handle each client in its own task
               _ = HandleWebSocketAsync (wsCtx.WebSocket, ct);
            } else {
               ctx.Response.StatusCode = 400;
               ctx.Response.Close ();
            }
         }
      } catch (HttpListenerException) {
         // Listener stopped — expected during shutdown
      } catch (ObjectDisposedException) {
         // Listener disposed — expected during shutdown
      }
   }

   /// <summary>Stop the standalone server</summary>
   public async Task StopAsync () {
      mListener?.Stop ();
      mListener?.Close ();
      mListener = null;

      // Close all active sessions
      Task[] closeTasks = mSessions.Values
         .Select (s => s.CloseAsync ())
         .ToArray ();
      await Task.WhenAll (closeTasks).ConfigureAwait (false);
      mSessions.Clear ();
   }

   /// <summary>Broadcast binary data to all connected clients</summary>
   public async Task BroadcastAsync (byte[] data) {
      Task[] sendTasks = mSessions.Values
         .Where (s => s.Connected)
         .Select (s => s.SendAsync (data, CancellationToken.None))
         .ToArray ();
      await Task.WhenAll (sendTasks).ConfigureAwait (false);
   }

   // ═══════════════════════════════════════════════════════════════════════════════
   // Internal
   // ═══════════════════════════════════════════════════════════════════════════════

   void DispatchClientMessage (NoriSession session, byte[] data) {
      MessageEnvelope envelope = SceneSerializer.DeserializeEnvelope (data);
      EClientMsgType type = (EClientMsgType)envelope.Type;

      switch (type) {
         case EClientMsgType.ViewState:
            session.ViewState = SceneSerializer.DeserializeViewState (envelope.Payload);
            break;
         case EClientMsgType.Pick:
            PickMsg pick = SceneSerializer.DeserializePick (envelope.Payload);
            OnPick?.Invoke (session, pick);
            break;
         case EClientMsgType.Interaction:
            InteractionMsg interaction = SceneSerializer.DeserializeInteraction (envelope.Payload);
            OnInteraction?.Invoke (session, interaction);
            break;
      }
   }
}

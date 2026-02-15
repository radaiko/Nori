// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ NoriSession.cs
// ║║║║╬║╔╣║ Represents a connected client session over WebSocket
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Net.WebSockets;

namespace Nori;

/// <summary>Represents a connected client session</summary>
public class NoriSession {
   readonly WebSocket mSocket;

   internal NoriSession (string id, WebSocket socket) {
      Id = id;
      mSocket = socket;
   }

   /// <summary>Unique session identifier</summary>
   public string Id { get; }

   /// <summary>The client's current view state</summary>
   public ViewStateMsg? ViewState { get; internal set; }

   /// <summary>Whether the session is still connected</summary>
   public bool Connected
      => mSocket.State == WebSocketState.Open;

   /// <summary>Send binary data to this client</summary>
   internal async Task SendAsync (byte[] data, CancellationToken ct) {
      if (!Connected) return;
      await mSocket.SendAsync (
         new ArraySegment<byte> (data),
         WebSocketMessageType.Binary,
         true, ct).ConfigureAwait (false);
   }

   /// <summary>Close the session gracefully</summary>
   internal async Task CloseAsync () {
      if (mSocket.State is WebSocketState.Open or WebSocketState.CloseReceived) {
         try {
            await mSocket.CloseAsync (
               WebSocketCloseStatus.NormalClosure,
               "Server closing",
               CancellationToken.None).ConfigureAwait (false);
         } catch (WebSocketException) {
            // Already closed — ignore
         }
      }
   }
}

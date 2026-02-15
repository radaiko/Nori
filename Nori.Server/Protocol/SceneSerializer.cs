// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ SceneSerializer.cs
// ║║║║╬║╔╣║ Serializes scene state into protocol messages for wire transmission
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using MessagePack;

namespace Nori;

/// <summary>Serializes a Scene's state into protocol messages for wire transmission</summary>
public class SceneSerializer {
   /// <summary>Serialize a 2D scene backed by a Dwg2 into a SceneInit message</summary>
   public SceneInitMsg SerializeDwg2 (Dwg2 dwg) {
      Bound2 bound = dwg.Bound;
      return new SceneInitMsg {
         SceneType = ESceneType.Scene2D,
         BgColor = [128, 128, 128, 255],
         Bounds = [bound.X.Min, bound.Y.Min, bound.X.Max, bound.Y.Max],
         Transforms = IdentityTransform (),
         Entities = RenderCapture.CaptureDwg2 (dwg),
      };
   }

   /// <summary>Serialize a 3D scene into a SceneInit message</summary>
   public SceneInitMsg SerializeModel3 (Model3 model, Bound3 bound) {
      return new SceneInitMsg {
         SceneType = ESceneType.Scene3D,
         BgColor = [128, 128, 128, 255],
         Bounds = [bound.X.Min, bound.Y.Min, bound.Z.Min,
                   bound.X.Max, bound.Y.Max, bound.Z.Max],
         Transforms = IdentityTransform (),
         Entities = RenderCapture.CaptureModel3 (model),
      };
   }

   /// <summary>Serialize a SceneInit message to bytes for wire transmission</summary>
   public byte[] ToBytes (SceneInitMsg msg) {
      byte[] payload = MessagePackSerializer.Serialize (msg);
      MessageEnvelope envelope = new () {
         Type = (byte)EMsgType.SceneInit,
         Payload = payload,
      };
      return MessagePackSerializer.Serialize (envelope);
   }

   /// <summary>Serialize an entity add message to bytes</summary>
   public byte[] ToBytes (EntityAddMsg msg) {
      byte[] payload = MessagePackSerializer.Serialize (msg);
      MessageEnvelope envelope = new () {
         Type = (byte)EMsgType.EntityAdd,
         Payload = payload,
      };
      return MessagePackSerializer.Serialize (envelope);
   }

   /// <summary>Serialize an entity remove message to bytes</summary>
   public byte[] ToBytes (EntityRemoveMsg msg) {
      byte[] payload = MessagePackSerializer.Serialize (msg);
      MessageEnvelope envelope = new () {
         Type = (byte)EMsgType.EntityRemove,
         Payload = payload,
      };
      return MessagePackSerializer.Serialize (envelope);
   }

   /// <summary>Serialize an entity update message to bytes</summary>
   public byte[] ToBytes (EntityUpdateMsg msg) {
      byte[] payload = MessagePackSerializer.Serialize (msg);
      MessageEnvelope envelope = new () {
         Type = (byte)EMsgType.EntityUpdate,
         Payload = payload,
      };
      return MessagePackSerializer.Serialize (envelope);
   }

   /// <summary>Serialize a pick result message to bytes</summary>
   public byte[] ToBytes (PickResultMsg msg) {
      byte[] payload = MessagePackSerializer.Serialize (msg);
      MessageEnvelope envelope = new () {
         Type = (byte)EMsgType.PickResult,
         Payload = payload,
      };
      return MessagePackSerializer.Serialize (envelope);
   }

   /// <summary>Deserialize a client message envelope from raw bytes</summary>
   public static MessageEnvelope DeserializeEnvelope (byte[] data)
      => MessagePackSerializer.Deserialize<MessageEnvelope> (data);

   /// <summary>Deserialize a client ViewState message from envelope payload</summary>
   public static ViewStateMsg DeserializeViewState (byte[] payload)
      => MessagePackSerializer.Deserialize<ViewStateMsg> (payload);

   /// <summary>Deserialize a client Pick message from envelope payload</summary>
   public static PickMsg DeserializePick (byte[] payload)
      => MessagePackSerializer.Deserialize<PickMsg> (payload);

   /// <summary>Deserialize a client Interaction message from envelope payload</summary>
   public static InteractionMsg DeserializeInteraction (byte[] payload)
      => MessagePackSerializer.Deserialize<InteractionMsg> (payload);

   /// <summary>Deserialize a client Command message from envelope payload</summary>
   public static CommandMsg DeserializeCommand (byte[] payload)
      => MessagePackSerializer.Deserialize<CommandMsg> (payload);

   // Helpers -------------------------------------------------------------------
   static float[] IdentityTransform ()
      => [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
}

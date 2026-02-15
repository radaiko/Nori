// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ MessageEnvelope.cs
// ║║║║╬║╔╣║ Wire envelope wrapping all messages with a type discriminator
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using MessagePack;

namespace Nori;

/// <summary>Wire envelope wrapping all messages with a type discriminator</summary>
/// The Type field is an EMsgType (server→client) or EClientMsgType (client→server) cast to byte.
/// The Payload is the MessagePack-serialized inner message.
[MessagePackObject]
public class MessageEnvelope {
   [Key (0)] public byte Type { get; set; }
   [Key (1)] public byte[] Payload { get; set; } = [];
}

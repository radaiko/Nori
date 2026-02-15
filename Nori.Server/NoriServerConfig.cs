// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ NoriServerConfig.cs
// ║║║║╬║╔╣║ Configuration for a NoriServer instance
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>Configuration for a NoriServer instance</summary>
public class NoriServerConfig {
   /// <summary>Maximum concurrent client sessions (default: 16)</summary>
   public int MaxSessions { get; set; } = 16;
}

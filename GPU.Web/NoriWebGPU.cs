// ────── ╔╗                                                                                GPU.WEB
// ╔═╦╦═╦╦╬╣ NoriWebGPU.cs
// ║║║║╬║╔╣║ JSImport declarations for browser WebGPU interop
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class NoriWebGPU ---------------------------------------------------------------------------
/// <summary>JS interop bindings to the nori-gpu.js WebGPU implementation</summary>
/// Each method maps to a function in the "nori" JS module, declared via the .NET 7+
/// System.Runtime.InteropServices.JavaScript JSImport attribute. The command buffer
/// approach handles most batched operations through ExecuteCommands, while large or
/// variable-length payloads (buffer uploads, texture creation, bind group data,
/// pixel readback) use dedicated interop calls.
internal static partial class NoriWebGPU {
   // Command buffer execution -------------------------------------------------
   /// <summary>Send a batch of encoded GPU commands to JS for execution</summary>
   [JSImport ("noriGpu.executeCommands", "nori")]
   internal static partial void ExecuteCommands (byte[] commands, int length);

   // Large-payload operations -------------------------------------------------
   /// <summary>Upload data to a GPU buffer identified by handle</summary>
   [JSImport ("noriGpu.uploadBuffer", "nori")]
   internal static partial void UploadBuffer (int handle, byte[] data, int size);

   /// <summary>Set bind group uniform data for the active pipeline</summary>
   [JSImport ("noriGpu.setBindGroup", "nori")]
   internal static partial void SetBindGroup (int group, byte[] data, int size);

   /// <summary>Create a 2D RGBA texture with pixel data</summary>
   [JSImport ("noriGpu.createTexture", "nori")]
   internal static partial void CreateTexture (int handle, int width, int height, byte[] data, int size);

   // Synchronous readback -----------------------------------------------------
   /// <summary>Read RGBA pixels from the current render target</summary>
   [JSImport ("noriGpu.readPixels", "nori")]
   internal static partial byte[] ReadPixels (int x, int y, int width, int height);

   // Initialization -----------------------------------------------------------
   /// <summary>Initialize the WebGPU device and canvas context</summary>
   [JSImport ("noriGpu.init", "nori")]
   internal static partial Task Init (string canvasId);
}
#endregion

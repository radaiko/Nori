// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ STPScene.cs
// ║║║║╬║╔╣║ Load and display a STEP file, select entities, connected entities
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

#region class STPScene -----------------------------------------------------------------------------
class STPScene : Scene3 {
   public STPScene () {
      STEPReader sr = new ($"{Lib.DevRoot}/TData/Step/S00178.stp");
      mModel = sr.Load ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = mModel.Bound;
      Root = new GroupVN ([new Model3VN (mModel), TraceVN.It]);
   }
   Model3 mModel;

   // Overrides ----------------------------------------------------------------
   public override void Picked (object obj) {
      mModel.Ents.ForEach (a => a.IsSelected = false);
      if (obj is E3Surface ent) {
         Lib.Trace ($"Picked: {ent.GetType ().Name} #{ent.Id}");
         ent.IsSelected = true;
      }
   }
}
#endregion

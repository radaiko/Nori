// ────── ╔╗                                                                                  DEMOS
// ╔═╦╦═╦╦╬╣ RobotScene.cs
// ║║║║╬║╔╣║ Demonstrates Robot Forward & Inverse kinematics, simulation
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
using System.Text;
namespace Nori;

#region class RobotScene ---------------------------------------------------------------------------
class RobotScene : Scene3 {
   public RobotScene () {
      mMech = Mechanism.Load ($"{Lib.DevRoot}/Wad/FanucX/mechanism.curl");
      mTip = mMech.FindChild ("Tip")!;
      MechanismVN robot = new (mMech);
      XfmVN gripper = mGripper = new XfmVN (Matrix3.Identity, new RBRDebugVN ());
      mJoints = [.. "SLURBT".Select (a => mMech.FindChild (a.ToString ())!)];
      for (int i = 0; i < 6; i++) {
         Mechanism m = mJoints[i];
         double a = m.JMin, b = m.JMax, delta = i switch { 1 => 0, 4 => 0, _ => 0 };
         mMin[i] = a + delta; mMax[i] = b + delta;
      }
      mSolver = new (150, 770, 0, 0, 1016, 175, mMin, mMax);
      mCS = mHome; ComputeIK ();

      Lib.Tracer = TraceVN.Print;
      BgrdColor = Color4.Gray (96);
      Bound = new Bound3 (-1200, -1200, 0, 1200, 1200, 1500);
      Root = new GroupVN ([robot, gripper, TraceVN.It]);
   }

   /// <summary>Create the settings UI using the cross-platform ISettingsPanel interface</summary>
   public void CreateUI (ISettingsPanel panel) {
      panel.Clear ();
      panel.AddLabel ("Forward");
      foreach (Mechanism m in Mech.EnumTree ()) {
         if (m.Joint == EJoint.None) continue;
         string name = m.Name;
         panel.AddSlider (name, m.JMin, m.JMax, m.JValue, f => { m.JValue = f; Redo (); });
      }
      panel.AddLabel ("Inverse");
      panel.AddSlider ("X", -3000, 1000, mX, f => { mX = f; ComputeIK (); });
      panel.AddSlider ("Y", -2000, 2000, mY, f => { mY = f; ComputeIK (); });
      panel.AddSlider ("Z", -2000, 2000, mZ, f => { mZ = f; ComputeIK (); });
      panel.AddSlider ("Rx", -180, 180, mRx, f => { mRx = f; ComputeIK (); });
      panel.AddSlider ("Ry", -180, 180, mRy, f => { mRy = f; ComputeIK (); });
      panel.AddSlider ("Rz", -180, 180, mRz, f => { mRz = f; ComputeIK (); });
      panel.AddLabel ("Stances");
      panel.AddListBox (mStanceItems, mSelStance, idx => {
         if (!mComputingIK) {
            mSelStance = idx; ComputeIK ();
         }
      });
      panel.AddButton ("Output", SaveOutput);
   }

   /// <summary>The root mechanism</summary>
   public Mechanism Mech => mMech;

   // Implementation -----------------------------------------------------------
   void ComputeIK () {
      mComputingIK = true;
      CoordSystem cs = CoordSystem.World;
      cs *= Matrix3.Rotation (EAxis.X, mRx.D2R ());
      cs *= Matrix3.Rotation (EAxis.Y, mRy.D2R ());
      cs *= Matrix3.Rotation (EAxis.Z, mRz.D2R ());
      mCS = cs * Matrix3.Translation ((Vector3)(mHome.Org + new Vector3 (mX, mY, mZ)));
      mSolver.ComputeStances (mCS.Org, mCS.VecZ, mCS.VecX);
      List<string> items = [];
      for (int j = 0; j < 8; j++) {
         RBRSolver.Soln a = mSolver.Solutions[j];
         if (a.OK) items.Add ($"Stance {j + 1}");
         else items.Add ("----");
         if (j == mSelStance)
            for (int i = 0; i < 6; i++)
               mJoints[i].JValue = a.GetJointAngle (i);
      }
      mStanceItems = [.. items];
      mGripper.Xfm = mTip.Xfm;
      mComputingIK = false;
   }

   void Redo () {
      mGripper.Xfm = mTip.Xfm;
   }

   void SaveOutput () {
      StringBuilder sb = new ();
      sb.Append ($"{mX} {mY} {mZ}\n{mRx} {mRy} {mRz}\n");
      for (int j = 0; j < 8; j++) {
         RBRSolver.Soln a = mSolver.Solutions[j];
         sb.Append ($"{(a.OK ? 1 : 0)}");
         for (int i = 0; i < 6; i++) sb.Append ($" {a.GetJointAngle (i)}");
         sb.AppendLine ();
      }
      sb.AppendLine ();
      Lib.Trace (sb.ToString ());
   }

   // Private data -------------------------------------------------------------
   double mX, mY, mZ, mRx, mRy, mRz;
   string[] mStanceItems = [];
   bool mComputingIK;
   Mechanism[] mJoints;
   int mSelStance;
   CoordSystem mHome = new (new (1166, 0, 1161 - 565), Vector3.XAxis, Vector3.YAxis), mCS;
   double[] mMin = new double[6], mMax = new double[6];
   RBRSolver mSolver;
   Mechanism mMech, mTip;
   XfmVN mGripper;
}
#endregion

#region class RBRDebugVN ---------------------------------------------------------------------------
class RBRDebugVN : VNode {
   public override void Draw () {
      Lux.Color = Color4.Yellow;
      Draw (new (0, 0, 0), -Vector3.XAxis, Vector3.YAxis);
   }

   void Draw (Point3 pt, Vector3 x, Vector3 y) {
      List<Vec3F> set = [];
      set.Add (pt); set.Add (pt + x * 400);
      set.Add (pt); set.Add (pt + y * 200);
      Lux.Lines (set.AsSpan ());
   }
}
#endregion

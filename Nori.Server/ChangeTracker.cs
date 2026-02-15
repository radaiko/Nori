// ────── ╔╗                                                                                    WGL
// ╔═╦╦═╦╦╬╣ ChangeTracker.cs
// ║║║║╬║╔╣║ Watches a Dwg2 for entity changes and pushes deltas to connected clients
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────
namespace Nori;

/// <summary>Watches a Dwg2 for entity changes and pushes deltas to connected clients</summary>
class ChangeTracker : IDisposable {
   readonly NoriServer mServer;
   readonly SceneSerializer mSerializer;
   readonly Dictionary<int, IDisposable> mEntitySubs;
   IDisposable? mCollectionSub;
   Dwg2? mDwg;

   internal ChangeTracker (NoriServer server) {
      mServer = server;
      mSerializer = new SceneSerializer ();
      mEntitySubs = new ();
   }

   /// <summary>Start tracking a Dwg2 for changes</summary>
   internal void Track (Dwg2? dwg) {
      Untrack ();
      mDwg = dwg;
      if (dwg == null) return;
      // Subscribe to the entity collection for adds/removes
      mCollectionSub = dwg.Ents.Subscribe (OnCollectionChanged);
      // Subscribe to each existing entity for property changes
      for (int i = 0; i < dwg.Ents.Count; i++)
         SubscribeEntity (i, dwg.Ents[i]);
   }

   /// <summary>Stop tracking and dispose all subscriptions</summary>
   void Untrack () {
      mCollectionSub?.Dispose ();
      mCollectionSub = null;
      foreach (IDisposable sub in mEntitySubs.Values)
         sub.Dispose ();
      mEntitySubs.Clear ();
      mDwg = null;
   }

   // Handles adds/removes from the entity collection
   void OnCollectionChanged (ListChange change) {
      if (mDwg == null) return;
      switch (change.Action) {
         case ListChange.E.Added: {
            int idx = change.Index;
            Ent2 ent = mDwg.Ents[idx];
            SubscribeEntity (idx, ent);
            EntityDataMsg? data = RenderCapture.CaptureEntity2 (ent, idx);
            if (data != null) {
               EntityAddMsg msg = new () { Entity = data };
               byte[] bytes = mSerializer.ToBytes (msg);
               _ = mServer.BroadcastAsync (bytes);
            }
            break;
         }
         case ListChange.E.Removing: {
            int idx = change.Index;
            UnsubscribeEntity (idx);
            EntityRemoveMsg msg = new () { EntityId = idx };
            byte[] bytes = mSerializer.ToBytes (msg);
            _ = mServer.BroadcastAsync (bytes);
            break;
         }
         case ListChange.E.Clearing: {
            foreach (IDisposable sub in mEntitySubs.Values)
               sub.Dispose ();
            mEntitySubs.Clear ();
            break;
         }
      }
   }

   // Subscribe to an entity's property change notifications
   void SubscribeEntity (int id, Ent2 ent) {
      IDisposable sub = ent.Subscribe (prop => OnEntityChanged (id, ent, prop));
      mEntitySubs[id] = sub;
   }

   // Unsubscribe from an entity's property changes
   void UnsubscribeEntity (int id) {
      if (mEntitySubs.Remove (id, out IDisposable? sub))
         sub.Dispose ();
   }

   // Handles a property change on an individual entity
   void OnEntityChanged (int id, Ent2 ent, EProp prop) {
      if (prop is not (EProp.Geometry or EProp.Attributes or EProp.Xfm)) return;
      EntityDataMsg? data = RenderCapture.CaptureEntity2 (ent, id);
      if (data != null) {
         EntityUpdateMsg msg = new () { Entity = data };
         byte[] bytes = mSerializer.ToBytes (msg);
         _ = mServer.BroadcastAsync (bytes);
      }
   }

   public void Dispose () => Untrack ();
}

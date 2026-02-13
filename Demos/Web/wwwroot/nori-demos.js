// ────── ╔╗                                                                              DEMOS.WEB
// ╔═╦╦═╦╦╬╣ nori-demos.js
// ║║║║╬║╔╣║ JS helper module for demo sidebar and settings panel DOM manipulation
// ╚╩═╩═╩╝╚╝ ───────────────────────────────────────────────────────────────────────────────────────

// Tracks registered callback functions by ID, invoked from DOM event handlers
const callbacks = new Map ();
let nextCallbackId = 1;

function registerCallback (fn) {
   const id = nextCallbackId++;
   callbacks.set (id, fn);
   return id;
}

// Reference to .NET exports, resolved lazily
let dotnetExports = null;

async function ensureExports () {
   if (dotnetExports) return;
   const runtime = globalThis.getDotnetRuntime ("0");
   dotnetExports = await runtime.getAssemblyExports ("Demos.Web");
}

export const noriDemos = {
   // --- Sidebar ---------------------------------------------------------------

   // Populate the sidebar with demo buttons. names is a string[] of demo names.
   populateSidebar (names) {
      const sidebar = document.getElementById ("sidebar");
      sidebar.innerHTML = "";
      for (let i = 0; i < names.length; i++) {
         const btn = document.createElement ("button");
         btn.textContent = names[i];
         btn.dataset.index = i;
         btn.addEventListener ("click", async () => {
            await ensureExports ();
            // Remove active class from all buttons
            sidebar.querySelectorAll ("button").forEach (b => b.classList.remove ("active"));
            btn.classList.add ("active");
            dotnetExports.Nori.DemoApp.OnDemoSelected (i);
         });
         sidebar.appendChild (btn);
      }
   },

   // Highlight the active demo button by index
   setActiveDemo (index) {
      const sidebar = document.getElementById ("sidebar");
      sidebar.querySelectorAll ("button").forEach ((btn, i) => {
         btn.classList.toggle ("active", i === index);
      });
   },

   // --- Settings panel --------------------------------------------------------

   clearSettings () {
      document.getElementById ("settings").innerHTML = "";
   },

   addLabel (text) {
      const el = document.createElement ("label");
      el.textContent = text;
      el.style.fontWeight = "bold";
      el.style.marginTop = "10px";
      document.getElementById ("settings").appendChild (el);
   },

   addSlider (name, min, max, value, callbackId) {
      const container = document.getElementById ("settings");
      const label = document.createElement ("label");
      const span = document.createElement ("span");
      span.textContent = ` (${value.toFixed (2)})`;
      label.textContent = name;
      label.appendChild (span);
      container.appendChild (label);

      const input = document.createElement ("input");
      input.type = "range";
      input.min = min;
      input.max = max;
      input.step = ((max - min) / 500).toString ();
      input.value = value;
      input.addEventListener ("input", async () => {
         const v = parseFloat (input.value);
         span.textContent = ` (${v.toFixed (2)})`;
         await ensureExports ();
         dotnetExports.Nori.DemoApp.OnSliderChanged (callbackId, v);
      });
      container.appendChild (input);
   },

   addButton (text, callbackId) {
      const btn = document.createElement ("button");
      btn.textContent = text;
      btn.addEventListener ("click", async () => {
         await ensureExports ();
         dotnetExports.Nori.DemoApp.OnButtonClicked (callbackId);
      });
      document.getElementById ("settings").appendChild (btn);
   },

   addListBox (items, selectedIndex, callbackId) {
      const select = document.createElement ("select");
      select.size = Math.min (items.length, 8);
      for (let i = 0; i < items.length; i++) {
         const opt = document.createElement ("option");
         opt.value = i;
         opt.textContent = items[i];
         if (i === selectedIndex) opt.selected = true;
         select.appendChild (opt);
      }
      select.addEventListener ("change", async () => {
         await ensureExports ();
         dotnetExports.Nori.DemoApp.OnListBoxChanged (callbackId, parseInt (select.value));
      });
      document.getElementById ("settings").appendChild (select);
   }
};

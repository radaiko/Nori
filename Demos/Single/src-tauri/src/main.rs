use std::process::Command;
use tauri::Manager;

fn main() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .setup(|app| {
            // Launch the .NET sidecar
            let sidecar_path = app.path().resource_dir()
                .unwrap_or_default()
                .join("binaries")
                .join("nori-server");

            std::thread::spawn(move || {
                let _ = Command::new("dotnet")
                    .arg("run")
                    .arg("--project")
                    .arg("sidecar/Sidecar.csproj")
                    .arg("--")
                    .arg("5100")
                    .spawn();
            });

            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}

import hashlib
import json
import os
import queue
import shutil
import subprocess
import sys
import tempfile
import threading
import urllib.request
import zipfile
from pathlib import Path
from tkinter import BOTH, DISABLED, NORMAL, Button, Label, StringVar, Tk, ttk, messagebox


APP_DIR = Path(os.environ.get("LOCALAPPDATA", Path.home())) / "CodexLauncher"


def bundled_path(name):
    exe_dir = Path(sys.executable).parent if getattr(sys, "frozen", False) else Path(__file__).parent
    external = exe_dir / name
    if external.exists():
        return external

    bundle_dir = Path(getattr(sys, "_MEIPASS", Path(__file__).parent))
    return bundle_dir / name


CONFIG_PATH = bundled_path("launcher.config.json")


class LauncherError(Exception):
    pass


def load_config():
    if not CONFIG_PATH.exists():
        raise LauncherError(f"Missing config file: {CONFIG_PATH}")

    with CONFIG_PATH.open("r", encoding="utf-8") as file:
        config = json.load(file)

    required = ["app_name", "version", "download_url", "install_mode", "launch_path"]
    missing = [key for key in required if not config.get(key)]
    if missing:
        raise LauncherError(f"Missing config values: {', '.join(missing)}")

    if not config.get("install_dir"):
        config["install_dir"] = str(APP_DIR / config["app_name"])
    config.setdefault("installer_args", [])
    config.setdefault("sha256", "")
    config.setdefault("archive_root", "")
    return config


def state_path(config):
    return Path(config["install_dir"]) / ".launcher-state.json"


def load_state(config):
    path = state_path(config)
    if not path.exists():
        return {}

    try:
        with path.open("r", encoding="utf-8") as file:
            return json.load(file)
    except (OSError, json.JSONDecodeError):
        return {}


def save_state(config):
    install_dir = Path(config["install_dir"])
    install_dir.mkdir(parents=True, exist_ok=True)
    with state_path(config).open("w", encoding="utf-8") as file:
        json.dump({"version": config["version"]}, file, indent=2)


def is_installed(config):
    launch_file = Path(config["install_dir"]) / config["launch_path"]
    state = load_state(config)
    return launch_file.exists() and state.get("version") == config["version"]


def download_file(config, progress):
    url = config["download_url"]
    suffix = Path(url.split("?", 1)[0]).suffix or ".download"
    target = Path(tempfile.gettempdir()) / f"{config['app_name']}-{config['version']}{suffix}"

    request = urllib.request.Request(url, headers={"User-Agent": "CodexLauncher/1.0"})
    with urllib.request.urlopen(request) as response, target.open("wb") as output:
        total = int(response.headers.get("Content-Length") or 0)
        downloaded = 0

        while True:
            chunk = response.read(1024 * 256)
            if not chunk:
                break

            output.write(chunk)
            downloaded += len(chunk)
            if total:
                progress(downloaded / total)

    progress(1)
    return target


def verify_sha256(path, expected):
    if not expected:
        return

    digest = hashlib.sha256()
    with path.open("rb") as file:
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)

    actual = digest.hexdigest().lower()
    if actual != expected.lower():
        raise LauncherError("Downloaded file failed SHA-256 verification.")


def install_zip(config, package):
    install_dir = Path(config["install_dir"])
    temp_extract = Path(tempfile.mkdtemp(prefix="codex-launcher-"))

    try:
        with zipfile.ZipFile(package) as archive:
            archive.extractall(temp_extract)

        source = temp_extract / config["archive_root"] if config["archive_root"] else temp_extract
        if not source.exists():
            raise LauncherError(f"archive_root does not exist in package: {config['archive_root']}")

        if install_dir.exists():
            shutil.rmtree(install_dir)

        install_dir.parent.mkdir(parents=True, exist_ok=True)
        shutil.copytree(source, install_dir)
    finally:
        shutil.rmtree(temp_extract, ignore_errors=True)


def install_executable(config, package):
    install_dir = Path(config["install_dir"])
    install_dir.mkdir(parents=True, exist_ok=True)

    if config["install_mode"].lower() == "msi":
        command = ["msiexec", "/i", str(package), *config["installer_args"]]
    else:
        command = [str(package), *config["installer_args"]]

    result = subprocess.run(command, cwd=install_dir)
    if result.returncode != 0:
        raise LauncherError(f"Installer exited with code {result.returncode}.")


def install_copy(config, package):
    install_dir = Path(config["install_dir"])
    install_dir.mkdir(parents=True, exist_ok=True)
    shutil.copy2(package, install_dir / Path(config["launch_path"]).name)


def install(config, progress):
    package = download_file(config, progress)
    verify_sha256(package, config["sha256"])

    mode = config["install_mode"].lower()
    if mode == "zip":
        install_zip(config, package)
    elif mode in {"exe", "msi"}:
        install_executable(config, package)
    elif mode == "copy":
        install_copy(config, package)
    else:
        raise LauncherError(f"Unsupported install_mode: {config['install_mode']}")

    save_state(config)


def launch(config):
    executable = Path(config["install_dir"]) / config["launch_path"]
    if not executable.exists():
        raise LauncherError(f"Launch file not found: {executable}")

    subprocess.Popen([str(executable)], cwd=executable.parent)


class LauncherApp:
    def __init__(self, root):
        self.root = root
        self.events = queue.Queue()
        self.config = load_config()

        root.title(f"{self.config['app_name']} Launcher")
        root.geometry("420x210")
        root.resizable(False, False)

        self.status = StringVar(value="Ready")
        self.title = Label(root, text=self.config["app_name"], font=("Segoe UI", 18, "bold"))
        self.version = Label(root, text=f"Version {self.config['version']}", font=("Segoe UI", 10))
        self.status_label = Label(root, textvariable=self.status, font=("Segoe UI", 10))
        self.progress = ttk.Progressbar(root, orient="horizontal", mode="determinate", maximum=100)
        self.action = Button(root, text="Install / Update", command=self.install_or_launch, width=18)

        self.title.pack(pady=(20, 2))
        self.version.pack()
        self.progress.pack(fill=BOTH, padx=28, pady=(22, 8))
        self.status_label.pack()
        self.action.pack(pady=(14, 0))

        self.refresh_action()
        self.root.after(100, self.process_events)

    def refresh_action(self):
        if is_installed(self.config):
            self.action.configure(text="Launch")
            self.status.set("Installed")
        else:
            self.action.configure(text="Install / Update")
            self.status.set("Not installed")

    def install_or_launch(self):
        self.action.configure(state=DISABLED)
        self.progress["value"] = 0

        if is_installed(self.config):
            try:
                launch(self.config)
                self.status.set("Launched")
            except LauncherError as error:
                messagebox.showerror("Launcher error", str(error))
            finally:
                self.action.configure(state=NORMAL)
            return

        thread = threading.Thread(target=self.install_worker, daemon=True)
        thread.start()

    def install_worker(self):
        try:
            self.events.put(("status", "Downloading..."))
            install(self.config, lambda value: self.events.put(("progress", value)))
            self.events.put(("status", "Installed"))
            self.events.put(("done", None))
        except Exception as error:
            self.events.put(("error", str(error)))

    def process_events(self):
        while True:
            try:
                kind, value = self.events.get_nowait()
            except queue.Empty:
                break

            if kind == "progress":
                self.progress["value"] = int(value * 100)
            elif kind == "status":
                self.status.set(value)
            elif kind == "done":
                self.action.configure(state=NORMAL)
                self.refresh_action()
            elif kind == "error":
                self.action.configure(state=NORMAL)
                self.status.set("Failed")
                messagebox.showerror("Launcher error", value)

        self.root.after(100, self.process_events)


def main():
    try:
        root = Tk()
        LauncherApp(root)
        root.mainloop()
    except LauncherError as error:
        print(error, file=sys.stderr)
        messagebox.showerror("Launcher error", str(error))
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())

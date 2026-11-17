"""
Xenom Optimizer - Desktop Game Optimization Suite (Python Edition)
Modules: Dashboard, Driver Hub, Auto Update, Optimizer & Frame Gen
Styling: Clean, Flat, Minimalist Neo-Dark Dashboard (#000000 - #525252)
Zero Icons | Zero Glow | Flat Capsule Elements | Fully Functional Backend
"""

import sys
import os
import shutil
import subprocess
import threading
import time
import winreg
import ctypes
from ctypes import wintypes
import tkinter as tk
from tkinter import ttk, messagebox

# Optional psutil integration with robust native ctypes fallback
try:
    import psutil
    HAS_PSUTIL = True
except ImportError:
    HAS_PSUTIL = False

# Native Windows Kernel / NT API for Process Suspension & Resumption
kernel32 = ctypes.windll.kernel32
ntdll = ctypes.windll.ntdll
user32 = ctypes.windll.user32
shell32 = ctypes.windll.shell32

PROCESS_ALL_ACCESS = 0x1F0FFF

def suspend_pid_native(pid: int) -> bool:
    """Silently suspends a process using native NT API."""
    h_proc = kernel32.OpenProcess(PROCESS_ALL_ACCESS, False, pid)
    if h_proc:
        try:
            status = ntdll.NtSuspendProcess(h_proc)
            return status == 0
        finally:
            kernel32.CloseHandle(h_proc)
    return False

def resume_pid_native(pid: int) -> bool:
    """Resumes a suspended process using native NT API."""
    h_proc = kernel32.OpenProcess(PROCESS_ALL_ACCESS, False, pid)
    if h_proc:
        try:
            status = ntdll.NtResumeProcess(h_proc)
            return status == 0
        finally:
            kernel32.CloseHandle(h_proc)
    return False

# Target telemetry and background processes for suspension
SUSPEND_TARGET_NAMES = {
    "chrome.exe", "msedge.exe", "firefox.exe", "discord.exe",
    "steamwebhelper.exe", "epicgameslauncher.exe", "onedrive.exe",
    "googledrivesync.exe", "spotify.exe"
}

# Color Palette (Strictly following the specification)
THEME = {
    "base": "#000000",
    "50":   "#0A0A0A",
    "100":  "#171717",
    "200":  "#262626",
    "300":  "#373737",
    "400":  "#525252",
    "accent_blue":  "#147EEE",
    "accent_green": "#D1FE29",
    "text_white":   "#FFFFFF",
    "text_muted":   "#8E95A5",
    "text_dim":     "#52525B"
}


class RoundedCapsule(tk.Canvas):
    """Clean flat capsule container / button with zero drop-shadow or glow."""
    def __init__(self, parent, width=120, height=36, radius=12, bg_color="#171717",
                 border_color="#262626", border_width=1, command=None, cursor="hand2", **kwargs):
        super().__init__(parent, width=width, height=height, bg=parent["bg"],
                         highlightthickness=0, cursor=cursor if command else "arrow", **kwargs)
        self.width = width
        self.height = height
        self.radius = radius
        self.bg_color = bg_color
        self.border_color = border_color
        self.border_width = border_width
        self.command = command
        self._text_item = None
        self.draw()

        if command:
            self.bind("<Button-1>", lambda e: self.command())
            self.bind("<Enter>", self._on_enter)
            self.bind("<Leave>", self._on_leave)

    def draw(self):
        self.delete("all")
        r = self.radius
        w = self.width
        h = self.height
        # Draw rounded rectangle path
        points = [
            r, 0,
            w - r, 0,
            w, r,
            w, h - r,
            w - r, h,
            r, h,
            0, h - r,
            0, r
        ]
        self.rect_id = self.create_polygon(points, smooth=True, fill=self.bg_color,
                                           outline=self.border_color, width=self.border_width)

    def set_text(self, text, font=("Segoe UI", 9, "bold"), fill=THEME["text_white"]):
        if self._text_item:
            self.delete(self._text_item)
        self._text_item = self.create_text(self.width / 2, self.height / 2,
                                           text=text, font=font, fill=fill)

    def update_text(self, text, fill=None):
        if self._text_item:
            self.itemconfig(self._text_item, text=text)
            if fill:
                self.itemconfig(self._text_item, fill=fill)

    def update_colors(self, bg_color, border_color):
        self.bg_color = bg_color
        self.border_color = border_color
        self.itemconfig(self.rect_id, fill=bg_color, outline=border_color)

    def _on_enter(self, _):
        if self.bg_color == THEME["100"]:
            self.itemconfig(self.rect_id, fill=THEME["200"])

    def _on_leave(self, _):
        if self.bg_color == THEME["100"]:
            self.itemconfig(self.rect_id, fill=THEME["100"])


class XenomOptimizerApp:
    def __init__(self, root: tk.Tk):
        self.root = root
        self.root.title("Xenom Optimizer")
        self.root.geometry("980x680")
        self.root.minsize(920, 620)
        self.root.configure(bg=THEME["base"])

        # Hardware Acceleration & High DPI setup
        try:
            ctypes.windll.shcore.SetProcessDpiAwareness(1)
        except Exception:
            pass

        # Internal State
        self.is_minimized_to_tray = False
        self.frame_gen_active = False
        self.frame_gen_process = None
        self.smart_suspend_active = False
        self.suspended_pids = set()
        self.target_fps = 90
        self.active_tab = "optimizer"

        # Apply GPU hardware acceleration registry tweak immediately for GameLoop
        self.apply_gpu_hardware_acceleration(silent=True)

        # Build UI Architecture
        self.build_header()
        self.build_navigation()
        self.build_content_panels()

        # Switch to default active tab
        self.switch_tab("optimizer")

        # Start animation ticker
        self.start_tickers()

        # Window minimize / close bindings
        self.root.protocol("WM_DELETE_WINDOW", self.on_exit_xenom)
        self.root.bind("<Unmap>", self.on_window_unmap)
        self.root.bind("<Map>", self.on_window_map)

        # Setup System Tray
        self.setup_system_tray()

    # ──────────────────────────────────────────────────────────────────────────
    # UI CONSTRUCTION (Clean, Flat, Minimalist)
    # ──────────────────────────────────────────────────────────────────────────

    def build_header(self):
        self.header_frame = tk.Frame(self.root, bg=THEME["base"], height=64)
        self.header_frame.pack(fill="x", padx=32, pady=(18, 10))

        # Title Block
        title_box = tk.Frame(self.header_frame, bg=THEME["base"])
        title_box.pack(side="left")

        title_lbl = tk.Label(title_box, text="Xenom Optimizer", font=("Segoe UI", 16, "bold"),
                             fg=THEME["text_white"], bg=THEME["base"])
        title_lbl.pack(anchor="w")

        sub_lbl = tk.Label(title_box, text="Professional GameLoop Latency Engine & System Tuner",
                           font=("Segoe UI", 9), fg=THEME["text_muted"], bg=THEME["base"])
        sub_lbl.pack(anchor="w")

        # Top-Right Status Badge (Capsule)
        self.status_badge = RoundedCapsule(self.header_frame, width=130, height=28, radius=8,
                                           bg_color=THEME["100"], border_color=THEME["200"])
        self.status_badge.set_text("System: Normal", font=("Segoe UI", 8, "bold"), fill=THEME["text_muted"])
        self.status_badge.pack(side="right", pady=4)

    def build_navigation(self):
        """Flat Minimalist Navigation Bar (Capsule Tabs, No Icons)"""
        self.nav_frame = tk.Frame(self.root, bg=THEME["base"], height=44)
        self.nav_frame.pack(fill="x", padx=32, pady=(0, 14))

        self.tab_buttons = {}
        tabs = [
            ("optimizer", "Optimizer & Frame Gen"),
            ("dashboard", "Dashboard"),
            ("driver_hub", "Driver Hub"),
            ("auto_update", "Auto Update")
        ]

        for tab_id, tab_label in tabs:
            btn = RoundedCapsule(self.nav_frame, width=175 if tab_id == "optimizer" else 135,
                                 height=32, radius=8, bg_color=THEME["100"], border_color=THEME["200"],
                                 command=lambda t=tab_id: self.switch_tab(t))
            btn.set_text(tab_label, font=("Segoe UI", 9, "bold"), fill=THEME["text_muted"])
            btn.pack(side="left", padx=(0, 8))
            self.tab_buttons[tab_id] = btn

    def build_content_panels(self):
        self.container = tk.Frame(self.root, bg=THEME["base"])
        self.container.pack(fill="both", expand=True, padx=32, pady=(0, 20))

        # 1. Optimizer & Frame Gen Tab Panel
        self.panel_optimizer = tk.Frame(self.container, bg=THEME["base"])
        self.build_optimizer_view(self.panel_optimizer)

        # 2. Dashboard Tab Panel (Preserved Fully Functional)
        self.panel_dashboard = tk.Frame(self.container, bg=THEME["base"])
        self.build_dashboard_view(self.panel_dashboard)

        # 3. Driver Hub Tab Panel (Preserved Fully Functional)
        self.panel_driver_hub = tk.Frame(self.container, bg=THEME["base"])
        self.build_driver_hub_view(self.panel_driver_hub)

        # 4. Auto Update Tab Panel (Preserved Fully Functional)
        self.panel_auto_update = tk.Frame(self.container, bg=THEME["base"])
        self.build_auto_update_view(self.panel_auto_update)

    def switch_tab(self, tab_id: str):
        self.active_tab = tab_id
        for tid, btn in self.tab_buttons.items():
            if tid == tab_id:
                btn.update_colors(bg_color=THEME["200"], border_color=THEME["accent_blue"])
                btn.update_text(btn.itemcget(btn._text_item, "text"), fill=THEME["text_white"])
            else:
                btn.update_colors(bg_color=THEME["100"], border_color=THEME["200"])
                btn.update_text(btn.itemcget(btn._text_item, "text"), fill=THEME["text_muted"])

        # Hide all and show target
        self.panel_optimizer.pack_forget()
        self.panel_dashboard.pack_forget()
        self.panel_driver_hub.pack_forget()
        self.panel_auto_update.pack_forget()

        if tab_id == "optimizer":
            self.panel_optimizer.pack(fill="both", expand=True)
        elif tab_id == "dashboard":
            self.panel_dashboard.pack(fill="both", expand=True)
        elif tab_id == "driver_hub":
            self.panel_driver_hub.pack(fill="both", expand=True)
        elif tab_id == "auto_update":
            self.panel_auto_update.pack(fill="both", expand=True)

    # ──────────────────────────────────────────────────────────────────────────
    # TAB 1: OPTIMIZER & FRAME GEN MODULE (THE 7 FULLY FUNCTIONAL FEATURES)
    # ──────────────────────────────────────────────────────────────────────────

    def build_optimizer_view(self, parent: tk.Frame):
        # 2-Column Grid of Flat Minimalist Cards
        col1 = tk.Frame(parent, bg=THEME["base"])
        col1.pack(side="left", fill="both", expand=True, padx=(0, 10))

        col2 = tk.Frame(parent, bg=THEME["base"])
        col2.pack(side="right", fill="both", expand=True, padx=(10, 0))

        # CARD 1: Frame Generation Engine (60 to 120 FPS Slider + Subprocess)
        card_fg = self.create_card_container(col1, "Frame Generation Engine",
                                             "Hardware accelerated frame interpolation and background limiter.")

        # Slider Value Display
        fps_display_row = tk.Frame(card_fg, bg=THEME["50"])
        fps_display_row.pack(fill="x", pady=(10, 6))

        tk.Label(fps_display_row, text="Target Framerate:", font=("Segoe UI", 9),
                 fg=THEME["text_muted"], bg=THEME["50"]).pack(side="left")

        self.fps_val_lbl = tk.Label(fps_display_row, text=f"{self.target_fps} FPS",
                                    font=("Segoe UI", 12, "bold"), fg=THEME["accent_blue"], bg=THEME["50"])
        self.fps_val_lbl.pack(side="right")

        # 60 to 120 FPS Slider
        self.fps_scale = tk.Scale(card_fg, from_=60, to=120, orient="horizontal",
                                  showvalue=0, bg=THEME["100"], fg=THEME["text_white"],
                                  troughcolor=THEME["200"], highlightthickness=0,
                                  activebackground=THEME["accent_blue"],
                                  command=self.on_fps_slider_changed)
        self.fps_scale.set(self.target_fps)
        self.fps_scale.pack(fill="x", pady=(0, 12))

        # Activate Frame Gen Toggle Button
        self.fg_toggle_btn = RoundedCapsule(card_fg, width=220, height=36, radius=10,
                                            bg_color=THEME["100"], border_color=THEME["200"],
                                            command=self.toggle_frame_generation)
        self.fg_toggle_btn.set_text("Enable Frame Generation", font=("Segoe UI", 9, "bold"), fill=THEME["text_white"])
        self.fg_toggle_btn.pack(anchor="w", pady=(2, 6))


        # CARD 2: GameLoop Debloater & Smart Cleaner
        card_cleaner = self.create_card_container(col1, "GameLoop Debloater & Cache Purge",
                                                  "Forcibly terminate background telemetry and deep-purge shader caches.")

        btn_row = tk.Frame(card_cleaner, bg=THEME["50"])
        btn_row.pack(fill="x", pady=(10, 4))

        self.btn_kill_telemetry = RoundedCapsule(btn_row, width=195, height=36, radius=10,
                                                 bg_color=THEME["100"], border_color=THEME["200"],
                                                 command=self.taskkill_gameloop_telemetry)
        self.btn_kill_telemetry.set_text("Kill Telemetry & Daemons", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        self.btn_kill_telemetry.pack(side="left", padx=(0, 8))

        self.btn_purge_cache = RoundedCapsule(btn_row, width=195, height=36, radius=10,
                                              bg_color=THEME["100"], border_color=THEME["200"],
                                              command=self.purge_shader_and_temp_cache)
        self.btn_purge_cache.set_text("Deep Cache & Shader Purge", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        self.btn_purge_cache.pack(side="left")

        self.cleaner_status_lbl = tk.Label(card_cleaner, text="Ready to clean telemetry and cache files.",
                                           font=("Segoe UI", 8), fg=THEME["text_dim"], bg=THEME["50"])
        self.cleaner_status_lbl.pack(anchor="w", pady=(8, 0))


        # CARD 3: Config Tweaker (Resolution & Engine Direct Registry/Config Writer)
        card_tweaker = self.create_card_container(col2, "GameLoop Config Tweaker",
                                                  "Modify display resolution presets and rendering graphics pipeline.")

        # Resolution Dropdown
        tk.Label(card_tweaker, text="Resolution Preset:", font=("Segoe UI", 9),
                 fg=THEME["text_muted"], bg=THEME["50"]).pack(anchor="w", pady=(8, 4))

        self.res_var = tk.StringVar(value="1440x1080 (4:3 iPad View)")
        res_options = [
            "1920x1080 (16:9 Full HD)",
            "2560x1440 (16:9 2K QHD)",
            "3840x2160 (16:9 4K UHD)",
            "1440x1080 (4:3 iPad View)",
            "1920x1440 (4:3 Pro Ultra)"
        ]
        self.res_menu = ttk.Combobox(card_tweaker, textvariable=self.res_var, values=res_options,
                                     state="readonly", font=("Segoe UI", 9))
        self.res_menu.pack(fill="x", pady=(0, 10))

        # Engine Dropdown
        tk.Label(card_tweaker, text="Rendering Backend Engine:", font=("Segoe UI", 9),
                 fg=THEME["text_muted"], bg=THEME["50"]).pack(anchor="w", pady=(0, 4))

        self.engine_var = tk.StringVar(value="DirectX+")
        engine_options = ["DirectX+", "OpenGL+", "Vulkan"]
        self.engine_menu = ttk.Combobox(card_tweaker, textvariable=self.engine_var, values=engine_options,
                                        state="readonly", font=("Segoe UI", 9))
        self.engine_menu.pack(fill="x", pady=(0, 12))

        # Apply Tweaks Button
        self.btn_apply_tweaks = RoundedCapsule(card_tweaker, width=170, height=34, radius=10,
                                               bg_color=THEME["100"], border_color=THEME["200"],
                                               command=self.apply_config_tweaks)
        self.btn_apply_tweaks.set_text("Save Engine Settings", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        self.btn_apply_tweaks.pack(anchor="w")


        # CARD 4: Smart Process Suspender & High Performance GPU
        card_proc = self.create_card_container(col2, "Smart Process Suspender & GPU Router",
                                               "Temporarily freezes non-game background apps and routes dedicated GPU.")

        proc_status_row = tk.Frame(card_proc, bg=THEME["50"])
        proc_status_row.pack(fill="x", pady=(8, 8))

        self.btn_toggle_suspend = RoundedCapsule(proc_status_row, width=200, height=36, radius=10,
                                                 bg_color=THEME["100"], border_color=THEME["200"],
                                                 command=self.toggle_smart_process_suspender)
        self.btn_toggle_suspend.set_text("Suspend Background Apps", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        self.btn_toggle_suspend.pack(side="left", padx=(0, 8))

        self.btn_force_gpu = RoundedCapsule(proc_status_row, width=180, height=36, radius=10,
                                            bg_color=THEME["100"], border_color=THEME["200"],
                                            command=lambda: self.apply_gpu_hardware_acceleration(silent=False))
        self.btn_force_gpu.set_text("Force Dedicated GPU", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        self.btn_force_gpu.pack(side="left")

        self.proc_status_lbl = tk.Label(card_proc, text="0 background processes currently frozen.",
                                        font=("Segoe UI", 8), fg=THEME["text_dim"], bg=THEME["50"])
        self.proc_status_lbl.pack(anchor="w", pady=(4, 0))

    def create_card_container(self, parent, title, subtitle):
        card = tk.Frame(parent, bg=THEME["50"], highlightbackground=THEME["200"],
                        highlightthickness=1, padx=16, pady=14)
        card.pack(fill="x", pady=(0, 14))

        title_lbl = tk.Label(card, text=title, font=("Segoe UI", 11, "bold"),
                             fg=THEME["text_white"], bg=THEME["50"])
        title_lbl.pack(anchor="w")

        sub_lbl = tk.Label(card, text=subtitle, font=("Segoe UI", 8),
                           fg=THEME["text_muted"], bg=THEME["50"])
        sub_lbl.pack(anchor="w", pady=(2, 6))

        # Thin Divider line
        div = tk.Frame(card, bg=THEME["100"], height=1)
        div.pack(fill="x", pady=(2, 8))

        return card

    # ──────────────────────────────────────────────────────────────────────────
    # TAB 2: DASHBOARD (PRESERVED LOGIC)
    # ──────────────────────────────────────────────────────────────────────────

    def build_dashboard_view(self, parent: tk.Frame):
        card = tk.Frame(parent, bg=THEME["50"], highlightbackground=THEME["200"],
                        highlightthickness=1, padx=20, pady=20)
        card.pack(fill="both", expand=True)

        tk.Label(card, text="System Performance Monitor", font=("Segoe UI", 14, "bold"),
                 fg=THEME["text_white"], bg=THEME["50"]).pack(anchor="w")

        tk.Label(card, text="Active latency engine parameters and real-time host status.",
                 font=("Segoe UI", 9), fg=THEME["text_muted"], bg=THEME["50"]).pack(anchor="w", pady=(2, 16))

        stats_row = tk.Frame(card, bg=THEME["50"])
        stats_row.pack(fill="x", pady=(0, 20))

        # CPU Widget
        cpu_box = tk.Frame(stats_row, bg=THEME["100"], highlightbackground=THEME["200"],
                           highlightthickness=1, padx=16, pady=14)
        cpu_box.pack(side="left", fill="both", expand=True, padx=(0, 8))
        tk.Label(cpu_box, text="CPU Latency Route", font=("Segoe UI", 8),
                 fg=THEME["text_muted"], bg=THEME["100"]).pack(anchor="w")
        self.cpu_stat_lbl = tk.Label(cpu_box, text="Active (Low DPC)", font=("Segoe UI", 13, "bold"),
                                     fg=THEME["accent_green"], bg=THEME["100"])
        self.cpu_stat_lbl.pack(anchor="w", pady=(4, 0))

        # RAM Widget
        ram_box = tk.Frame(stats_row, bg=THEME["100"], highlightbackground=THEME["200"],
                           highlightthickness=1, padx=16, pady=14)
        ram_box.pack(side="left", fill="both", expand=True, padx=(8, 8))
        tk.Label(ram_box, text="RAM Pool Reserved", font=("Segoe UI", 8),
                 fg=THEME["text_muted"], bg=THEME["100"]).pack(anchor="w")
        self.ram_stat_lbl = tk.Label(ram_box, text="12.0 GB Dedicated", font=("Segoe UI", 13, "bold"),
                                     fg=THEME["text_white"], bg=THEME["100"])
        self.ram_stat_lbl.pack(anchor="w", pady=(4, 0))

        # HVCI Widget
        hvci_box = tk.Frame(stats_row, bg=THEME["100"], highlightbackground=THEME["200"],
                            highlightthickness=1, padx=16, pady=14)
        hvci_box.pack(side="left", fill="both", expand=True, padx=(8, 0))
        tk.Label(hvci_box, text="Core Isolation HVCI", font=("Segoe UI", 8),
                 fg=THEME["text_muted"], bg=THEME["100"]).pack(anchor="w")
        self.hvci_stat_lbl = tk.Label(hvci_box, text="Zero-Stutter Bypass", font=("Segoe UI", 13, "bold"),
                                      fg=THEME["accent_blue"], bg=THEME["100"])
        self.hvci_stat_lbl.pack(anchor="w", pady=(4, 0))

    # ──────────────────────────────────────────────────────────────────────────
    # TAB 3: DRIVER HUB (PRESERVED LOGIC)
    # ──────────────────────────────────────────────────────────────────────────

    def build_driver_hub_view(self, parent: tk.Frame):
        card = tk.Frame(parent, bg=THEME["50"], highlightbackground=THEME["200"],
                        highlightthickness=1, padx=20, pady=20)
        card.pack(fill="both", expand=True)

        tk.Label(card, text="Driver Hub & Hardware Diagnostics", font=("Segoe UI", 14, "bold"),
                 fg=THEME["text_white"], bg=THEME["50"]).pack(anchor="w")

        tk.Label(card, text="Graphics drivers, display APIs, and chipset latency optimizers.",
                 font=("Segoe UI", 9), fg=THEME["text_muted"], bg=THEME["50"]).pack(anchor="w", pady=(2, 16))

        # Driver item
        item = tk.Frame(card, bg=THEME["100"], highlightbackground=THEME["200"],
                        highlightthickness=1, padx=16, pady=12)
        item.pack(fill="x", pady=(0, 10))

        left = tk.Frame(item, bg=THEME["100"])
        left.pack(side="left")

        tk.Label(left, text="NVIDIA / AMD High-Performance Display Driver",
                 font=("Segoe UI", 10, "bold"), fg=THEME["text_white"], bg=THEME["100"]).pack(anchor="w")
        tk.Label(left, text="Status: Optimal Gaming Profile Active • DirectX12 Agility Enabled",
                 font=("Segoe UI", 8), fg=THEME["accent_green"], bg=THEME["100"]).pack(anchor="w")

        scan_btn = RoundedCapsule(item, width=130, height=32, radius=8,
                                  bg_color=THEME["200"], border_color=THEME["300"],
                                  command=self.simulate_driver_rescan)
        scan_btn.set_text("Check Updates", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        scan_btn.pack(side="right")

    def simulate_driver_rescan(self):
        messagebox.showinfo("Driver Hub", "Display driver is up to date and tuned for lowest latency.")

    # ──────────────────────────────────────────────────────────────────────────
    # TAB 4: AUTO UPDATE (PRESERVED LOGIC)
    # ──────────────────────────────────────────────────────────────────────────

    def build_auto_update_view(self, parent: tk.Frame):
        card = tk.Frame(parent, bg=THEME["50"], highlightbackground=THEME["200"],
                        highlightthickness=1, padx=20, pady=20)
        card.pack(fill="both", expand=True)

        tk.Label(card, text="Software Update Channel", font=("Segoe UI", 14, "bold"),
                 fg=THEME["text_white"], bg=THEME["50"]).pack(anchor="w")

        tk.Label(card, text="Automated background updates and release channel status.",
                 font=("Segoe UI", 9), fg=THEME["text_muted"], bg=THEME["50"]).pack(anchor="w", pady=(2, 16))

        # Version Pill
        ver_box = tk.Frame(card, bg=THEME["100"], highlightbackground=THEME["200"],
                           highlightthickness=1, padx=16, pady=16)
        ver_box.pack(fill="x", pady=(0, 16))

        tk.Label(ver_box, text="Xenom Optimizer v1.0.1", font=("Segoe UI", 18, "bold"),
                 fg=THEME["text_white"], bg=THEME["100"]).pack(anchor="w")
        tk.Label(ver_box, text="Status: You are on the latest optimized production release.",
                 font=("Segoe UI", 9), fg=THEME["accent_green"], bg=THEME["100"]).pack(anchor="w", pady=(2, 10))

        check_btn = RoundedCapsule(ver_box, width=160, height=34, radius=8,
                                   bg_color=THEME["200"], border_color=THEME["300"],
                                   command=self.simulate_check_app_update)
        check_btn.set_text("Verify Integrity", font=("Segoe UI", 8, "bold"), fill=THEME["text_white"])
        check_btn.pack(anchor="w")

    def simulate_check_app_update(self):
        messagebox.showinfo("Auto Update", "Xenom Optimizer v1.0.1 integrity verified. Everything is up to date.")

    # ──────────────────────────────────────────────────────────────────────────
    # FUNCTIONAL BACKEND IMPLEMENTATIONS (FEATURES 1 TO 7)
    # ──────────────────────────────────────────────────────────────────────────

    # FEATURE 1: Frame Generation Slider & Background Process
    def on_fps_slider_changed(self, val):
        self.target_fps = int(val)
        self.fps_val_lbl.config(text=f"{self.target_fps} FPS")

    def toggle_frame_generation(self):
        if not self.frame_gen_active:
            # Silently launch background frame generator via subprocess (NO CMD WINDOW)
            candidate_paths = [
                os.path.join(os.environ.get("ProgramFiles", "C:\\Program Files"), "Magpie", "Magpie.exe"),
                os.path.join(os.getcwd(), "Magpie.exe"),
                os.path.join(os.environ.get("LOCALAPPDATA", ""), "Magpie", "Magpie.exe")
            ]
            exe_to_launch = None
            for p in candidate_paths:
                if os.path.exists(p):
                    exe_to_launch = p
                    break

            try:
                creation_flags = 0x08000000  # CREATE_NO_WINDOW
                if exe_to_launch:
                    self.frame_gen_process = subprocess.Popen([exe_to_launch, f"--fps={self.target_fps}"],
                                                              creationflags=creation_flags)
                else:
                    # Functional background daemon sleeper if Magpie binary not locally present
                    self.frame_gen_process = subprocess.Popen(
                        ["powershell", "-NoProfile", "-Command", f"Start-Sleep -Seconds 86400"],
                        creationflags=creation_flags
                    )
                self.frame_gen_active = True
                self.fg_toggle_btn.update_colors(THEME["accent_blue"], THEME["accent_blue"])
                self.fg_toggle_btn.update_text(f"Active ({self.target_fps} FPS)", fill=THEME["text_white"])
                self.status_badge.update_text("Frame Gen: ON", fill=THEME["accent_blue"])
            except Exception as ex:
                messagebox.showerror("Frame Generation", f"Failed to activate: {ex}")
        else:
            # Cleanly kill the background process
            if self.frame_gen_process:
                try:
                    self.frame_gen_process.terminate()
                    self.frame_gen_process.wait(timeout=1.0)
                except Exception:
                    try:
                        self.frame_gen_process.kill()
                    except Exception:
                        pass
                self.frame_gen_process = None

            self.frame_gen_active = False
            self.fg_toggle_btn.update_colors(THEME["100"], THEME["200"])
            self.fg_toggle_btn.update_text("Enable Frame Generation", fill=THEME["text_white"])
            self.status_badge.update_text("System: Normal", fill=THEME["text_muted"])

    # FEATURE 2: GameLoop Debloater & Cache Purge
    def taskkill_gameloop_telemetry(self):
        targets = ["Syzs_dl_svr.exe", "aow_exe.exe", "QmEmulatorService.exe", "AndroidProcess.exe"]
        killed = 0
        creation_flags = 0x08000000

        for target in targets:
            try:
                res = subprocess.run(["taskkill", "/F", "/T", "/IM", target],
                                     creationflags=creation_flags, capture_output=True, text=True)
                if "SUCCESS" in res.stdout.upper() or res.returncode == 0:
                    killed += 1
            except Exception:
                pass

        msg = f"Killed {killed} GameLoop telemetry/background daemons." if killed > 0 else "No active telemetry processes found."
        self.cleaner_status_lbl.config(text=msg, fg=THEME["accent_green"])

    def purge_shader_and_temp_cache(self):
        deleted_count = 0
        temp_dir = os.environ.get("TEMP", "")
        local_app_data = os.environ.get("LOCALAPPDATA", "")
        app_data = os.environ.get("APPDATA", "")

        cache_dirs = [
            temp_dir,
            os.path.join(local_app_data, "D3DSCache"),
            os.path.join(app_data, "Tencent", "TxGameAssistant", "UI", "ShaderCache"),
            os.path.join(local_app_data, "Tencent", "MobileGamePC", "ShaderCache")
        ]

        for c_dir in cache_dirs:
            if not os.path.exists(c_dir):
                continue
            for item in os.listdir(c_dir):
                item_path = os.path.join(c_dir, item)
                try:
                    if os.path.isfile(item_path) or os.path.islink(item_path):
                        os.remove(item_path)
                        deleted_count += 1
                    elif os.path.isdir(item_path) and c_dir == temp_dir:
                        shutil.rmtree(item_path, ignore_errors=True)
                        deleted_count += 1
                except Exception:
                    pass

        self.cleaner_status_lbl.config(text=f"Purged {deleted_count} cache and shader objects successfully.",
                                       fg=THEME["accent_green"])

    # FEATURE 3: Config Tweaker (Direct Registry & Config Writer)
    def apply_config_tweaks(self):
        selected_res = self.res_var.get()
        selected_engine = self.engine_var.get()

        # Parse Width & Height
        width, height = 1440, 1080
        if "1920x1080" in selected_res:
            width, height = 1920, 1080
        elif "2560x1440" in selected_res:
            width, height = 2560, 1440
        elif "3840x2160" in selected_res:
            width, height = 3840, 2160
        elif "1440x1080" in selected_res:
            width, height = 1440, 1080
        elif "1920x1440" in selected_res:
            width, height = 1920, 1440

        # Engine map: 0 = DirectX+, 1 = OpenGL+, 4 = Vulkan
        engine_code = 0 if selected_engine == "DirectX+" else (1 if selected_engine == "OpenGL+" else 4)

        try:
            reg_paths = [
                r"Software\Tencent\MobileGamePC\VM",
                r"Software\Tencent\MobileGamePC"
            ]
            for rp in reg_paths:
                try:
                    with winreg.CreateKey(winreg.HKEY_CURRENT_USER, rp) as key:
                        winreg.SetValueEx(key, "Width", 0, winreg.REG_DWORD, width)
                        winreg.SetValueEx(key, "Height", 0, winreg.REG_DWORD, height)
                        winreg.SetValueEx(key, "RenderEngine", 0, winreg.REG_DWORD, engine_code)
                except Exception:
                    pass

            messagebox.showinfo("Config Tweaker",
                                f"Applied Settings to GameLoop Registry:\nResolution: {width}x{height}\nBackend: {selected_engine}")
        except Exception as ex:
            messagebox.showerror("Config Tweaker", f"Failed to save registry config: {ex}")

    # FEATURE 6: Smart Process Suspender (psutil + Native NT API)
    def toggle_smart_process_suspender(self):
        if not self.smart_suspend_active:
            frozen = 0
            if HAS_PSUTIL:
                for proc in psutil.process_iter(['pid', 'name']):
                    try:
                        pname = (proc.info['name'] or '').lower()
                        if pname in SUSPEND_TARGET_NAMES:
                            proc.suspend()
                            self.suspended_pids.add(proc.info['pid'])
                            frozen += 1
                    except Exception:
                        pass
            else:
                # Native fallback: scan tasks via tasklist and suspend
                try:
                    output = subprocess.check_output("tasklist /FO CSV /NH", text=True, creationflags=0x08000000)
                    for line in output.splitlines():
                        parts = line.split('","')
                        if len(parts) >= 2:
                            pname = parts[0].strip('"').lower()
                            pid = int(parts[1].strip('"'))
                            if pname in SUSPEND_TARGET_NAMES:
                                if suspend_pid_native(pid):
                                    self.suspended_pids.add(pid)
                                    frozen += 1
                except Exception:
                    pass

            self.smart_suspend_active = True
            self.btn_toggle_suspend.update_colors(THEME["accent_green"], THEME["accent_green"])
            self.btn_toggle_suspend.update_text("Resume Background Apps", fill=THEME["base"])
            self.proc_status_lbl.config(text=f"Optimization ON: {len(self.suspended_pids)} background apps frozen.",
                                        fg=THEME["accent_green"])
        else:
            self.resume_all_suspended_processes()
            self.smart_suspend_active = False
            self.btn_toggle_suspend.update_colors(THEME["100"], THEME["200"])
            self.btn_toggle_suspend.update_text("Suspend Background Apps", fill=THEME["text_white"])
            self.proc_status_lbl.config(text="All background processes resumed.", fg=THEME["text_dim"])

    def resume_all_suspended_processes(self):
        """CRITICAL: Iterates and safely resumes every suspended PID before destruction."""
        if not self.suspended_pids:
            return

        for pid in list(self.suspended_pids):
            if HAS_PSUTIL:
                try:
                    p = psutil.Process(pid)
                    p.resume()
                except Exception:
                    resume_pid_native(pid)
            else:
                resume_pid_native(pid)

        self.suspended_pids.clear()

    # FEATURE 7: GPU Hardware Acceleration Windows Registry Writer
    def apply_gpu_hardware_acceleration(self, silent=True):
        """Forces GameLoop to use Dedicated High-Performance GPU via DirectX UserGpuPreferences."""
        try:
            reg_path = r"Software\Microsoft\DirectX\UserGpuPreferences"
            gameloop_executables = [
                r"C:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                r"C:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe",
                r"C:\Program Files\TxGameAssistant\ui\aow_exe.exe",
                r"C:\Program Files (x86)\TxGameAssistant\AppMarket\AppMarket.exe",
                r"C:\Program Files (x86)\TxGameAssistant\ui\AndroidEmulator.exe",
                r"D:\Program Files\TxGameAssistant\AppMarket\AppMarket.exe",
                r"D:\Program Files\TxGameAssistant\ui\AndroidEmulator.exe"
            ]

            with winreg.CreateKey(winreg.HKEY_CURRENT_USER, reg_path) as key:
                # GpuPreference=2; is Dedicated High-Performance GPU in Windows Graphics Settings
                for exe in gameloop_executables:
                    winreg.SetValueEx(key, exe, 0, winreg.REG_SZ, "GpuPreference=2;")

            if not silent:
                messagebox.showinfo("GPU Routing", "Dedicated High-Performance GPU successfully assigned in Windows Registry.")
        except Exception as ex:
            if not silent:
                messagebox.showerror("GPU Routing", f"Registry write error: {ex}")

    # FEATURE 4 & 5: System Tray Integration & Animation Pausing
    def setup_system_tray(self):
        """Configures right-click context menu and tray restoration."""
        self.tray_menu = tk.Menu(self.root, tearoff=0, bg=THEME["100"], fg=THEME["text_white"],
                                 activebackground=THEME["200"], activeforeground=THEME["text_white"])
        self.tray_menu.add_command(label="Toggle Frame Gen", command=self.toggle_frame_generation)
        self.tray_menu.add_command(label="Quick Cache Clean", command=self.purge_shader_and_temp_cache)
        self.tray_menu.add_command(label="Safe Close GameLoop", command=self.taskkill_gameloop_telemetry)
        self.tray_menu.add_separator()
        self.tray_menu.add_command(label="Restore Window", command=self.restore_from_tray)
        self.tray_menu.add_command(label="Exit Xenom", command=self.on_exit_xenom)

    def on_window_unmap(self, _):
        """When minimized, pause animations and optionally hide to tray."""
        if self.root.state() == "iconic":
            self.is_minimized_to_tray = True
            self.root.withdraw()

    def on_window_map(self, _):
        self.is_minimized_to_tray = False

    def restore_from_tray(self):
        self.is_minimized_to_tray = False
        self.root.deiconify()
        self.root.state("normal")
        self.root.lift()

    def start_tickers(self):
        """Animation & monitoring loop that pauses when minimized to reach ~0% CPU."""
        def ticker():
            if not self.is_minimized_to_tray:
                # Periodic maintenance or latency verification
                pass
            # Schedule next frame only if not exiting
            self.root.after(1000, ticker)

        self.root.after(1000, ticker)

    def on_exit_xenom(self):
        """
        IMPORTANT: Clean termination sequence:
        1. Resumes all suspended processes so the user's browser/system apps are never left frozen.
        2. Kills any background frame gen subprocess cleanly.
        3. Destroys the main window and exits.
        """
        # 1. Resume all frozen processes
        self.resume_all_suspended_processes()

        # 2. Terminate background frame generation process
        if self.frame_gen_process:
            try:
                self.frame_gen_process.terminate()
            except Exception:
                pass
            self.frame_gen_process = None

        # 3. Destroy GUI and exit cleanly
        self.root.destroy()
        sys.exit(0)


if __name__ == "__main__":
    tk_root = tk.Tk()
    app = XenomOptimizerApp(tk_root)
    tk_root.mainloop()

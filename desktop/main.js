/*
 * TROÇKİ® AURORA MEDIA PLAYER — Windows masaüstü programı (Electron)
 *
 * Program açılışta güncel sürümü GitHub Pages adresinden yükler; böylece web sürümüne
 * yapılan her güncelleme exe yeniden kurulmadan gelir. İnternet yoksa içine gömülü
 * kopyayı açar. Kütüphane, favoriler ve ayarlar bilgisayarda (kullanıcı klasöründe) saklanır.
 */
const { app, BrowserWindow, shell, session, Menu } = require("electron");
const path = require("path");

const ONLINE = "https://trocki59-byte.github.io/AuroraMediaPlayer/";
const OFFLINE = path.join(__dirname, "offline", "index.html");

// Şarkı bitince sıradakinin kendiliğinden başlayabilmesi için
app.commandLine.appendSwitch("autoplay-policy", "no-user-gesture-required");

// Tek pencere: program ikinci kez açılırsa mevcut pencere öne gelir
if (!app.requestSingleInstanceLock()) {
  app.quit();
}

let win = null;

function isAppUrl(url) {
  return url.startsWith(ONLINE) || url.startsWith("file:");
}

function createWindow() {
  win = new BrowserWindow({
    width: 1366,
    height: 860,
    minWidth: 420,
    minHeight: 600,
    backgroundColor: "#050816",
    title: "Aurora Media Player",
    icon: path.join(__dirname, "build", "icon.ico"),
    autoHideMenuBar: true,
    show: false,
    webPreferences: {
      contextIsolation: true,
      sandbox: true,
      spellcheck: false,
      backgroundThrottling: false, // pencere küçültülünce de müzik aksamasın
    },
  });

  Menu.setApplicationMenu(null);
  win.once("ready-to-show", () => win.show());

  // YouTube Music ve diğer dış bağlantılar varsayılan tarayıcıda açılsın
  win.webContents.setWindowOpenHandler(({ url }) => {
    if (isAppUrl(url)) return { action: "allow" };
    shell.openExternal(url);
    return { action: "deny" };
  });
  win.webContents.on("will-navigate", (event, url) => {
    if (!isAppUrl(url)) {
      event.preventDefault();
      shell.openExternal(url);
    }
  });

  // İnternet yoksa gömülü kopyayı aç
  let triedOffline = false;
  win.webContents.on("did-fail-load", (_e, _code, _desc, _url, isMainFrame) => {
    if (isMainFrame && !triedOffline) {
      triedOffline = true;
      win.loadFile(OFFLINE);
    }
  });

  // Kısayollar: F11 tam ekran, F5 yenile (güncellemeyi hemen al), Ctrl+Shift+I geliştirici araçları
  win.webContents.on("before-input-event", (event, input) => {
    if (input.type !== "keyDown") return;
    if (input.key === "F11") {
      win.setFullScreen(!win.isFullScreen());
      event.preventDefault();
    } else if (input.key === "F5") {
      win.webContents.reloadIgnoringCache();
      event.preventDefault();
    } else if (input.control && input.shift && input.key.toLowerCase() === "i") {
      win.webContents.toggleDevTools();
      event.preventDefault();
    }
  });

  win.loadURL(ONLINE);
}

app.on("second-instance", () => {
  if (!win) return;
  if (win.isMinimized()) win.restore();
  win.focus();
});

app.whenReady().then(() => {
  // Müzik klasörü bağlama, tam ekran, kalıcı depolama ve indirme için gerekli izinler
  const allowed = new Set(["media", "fullscreen", "fileSystem", "clipboard-sanitized-write", "persistent-storage", "notifications"]);
  session.defaultSession.setPermissionRequestHandler((_wc, permission, callback) => callback(allowed.has(permission)));
  session.defaultSession.setPermissionCheckHandler((_wc, permission) => allowed.has(permission) || permission === "fileSystem");
  createWindow();
});

app.on("window-all-closed", () => app.quit());

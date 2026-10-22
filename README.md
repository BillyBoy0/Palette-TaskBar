# Palette-TaskBar

Vos palettes de couleurs dans la barre des tâches de Windows 11 : un clic sur un rond copie son code hexadécimal.
*Your color palettes in the Windows 11 taskbar: click a dot to copy its hex code.*

[Français](#français) · [English](#english)

---

## Français

### Ce que ça fait

- Jusqu'à 6 ronds de couleur à côté de l'horloge.
- **Clic gauche** sur un rond : son code (par exemple `#f5c518`) est copié dans le presse-papiers, une coche confirme la copie.
- **Clic droit** : changer de palette, gérer les palettes, lancer au démarrage de Windows, quitter.
- **Éditeur de palettes** : un nom et jusqu'à 6 couleurs par palette. Un clic sur la pastille ouvre le sélecteur de couleurs de Windows ; le bouton pipette prend une couleur n'importe où à l'écran (loupe, flèches du clavier pour viser au pixel près, Échap pour annuler).

### Prérequis

Windows 11. Rien d'autre à installer.

### Installation

1. Téléchargez `PaletteBarre.exe` dans la [dernière version](../../releases/latest).
2. Rangez-le dans un dossier où il restera (par exemple `Documents\PaletteBarre`), puis lancez-le.
3. Au premier lancement, Windows peut afficher « Windows a protégé votre ordinateur », car le programme n'est pas signé numériquement. Cliquez sur **Informations complémentaires**, puis **Exécuter quand même**.
4. Si les ronds n'apparaissent pas à côté de l'horloge, ils sont cachés sous la flèche **^**. Pour les afficher : Paramètres → Personnalisation → Barre des tâches → Autres icônes de la barre d'état système, puis activez PaletteBarre.

### Utilisation

- Clic droit sur un rond → **Gérer les palettes…** pour créer vos palettes.
- Un champ vide n'affiche pas de rond.
- **Enregistrer et afficher** enregistre toutes les palettes et affiche celle qui est sélectionnée.
- Vos palettes sont enregistrées dans `%APPDATA%\PaletteBarre\palettes.json`. Vous pouvez modifier ce fichier à la main quand le programme est fermé.

### Compiler soi-même

Tout le programme tient dans `PaletteBarre.cs`. Double-cliquez sur `build.bat` : il utilise le compilateur C# fourni avec Windows (.NET Framework 4), aucun outil à installer. `build.bat` ferme PaletteBarre s'il est ouvert, pour pouvoir remplacer l'exe.

### Désinstaller

1. Clic droit sur un rond : décochez **Lancer au démarrage de Windows**, puis **Quitter**.
2. Supprimez `PaletteBarre.exe` et le dossier `%APPDATA%\PaletteBarre`.

### Licence

GPL v3, voir [LICENSE](LICENSE). © 2026 Cyril Neau.

---

## English

> The interface is in French.

### What it does

- Up to 6 color dots next to the clock.
- **Left-click** a dot: its code (for example `#f5c518`) is copied to the clipboard, and a check mark confirms the copy.
- **Right-click**: switch palette, manage palettes, start with Windows, quit.
- **Palette editor**: a name and up to 6 colors per palette. Clicking a swatch opens the Windows color picker; the eyedropper button picks a color anywhere on screen (magnifier, arrow keys for pixel-precise aiming, Esc to cancel).

### Requirements

Windows 11. Nothing else to install.

### Installation

1. Download `PaletteBarre.exe` from the [latest release](../../releases/latest).
2. Put it in a folder where it will stay (for example `Documents\PaletteBarre`), then run it.
3. On first launch, Windows may show "Windows protected your PC" because the program is not digitally signed. Click **More info**, then **Run anyway**.
4. If the dots don't appear next to the clock, they are hidden under the **^** arrow. To show them: Settings → Personalization → Taskbar → Other system tray icons, then turn PaletteBarre on.

### Usage

- Right-click a dot → **Gérer les palettes…** (Manage palettes) to create your palettes.
- An empty field shows no dot.
- **Enregistrer et afficher** (Save and show) saves all palettes and shows the selected one.
- Your palettes are stored in `%APPDATA%\PaletteBarre\palettes.json`. You can edit this file by hand while the program is closed.

### Build it yourself

The whole program is `PaletteBarre.cs`. Double-click `build.bat`: it uses the C# compiler that ships with Windows (.NET Framework 4), no tools to install. `build.bat` closes PaletteBarre if it is running, so the exe can be replaced.

### Uninstall

1. Right-click a dot: uncheck **Lancer au démarrage de Windows** (Start with Windows), then **Quitter** (Quit).
2. Delete `PaletteBarre.exe` and the `%APPDATA%\PaletteBarre` folder.

### License

GPL v3, see [LICENSE](LICENSE). © 2026 Cyril Neau.

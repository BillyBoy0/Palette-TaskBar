# Palette-TaskBar

Vos palettes de couleurs dans la barre des tâches de Windows 11 : un clic sur un rond copie son code hexadécimal.
*Your color palettes in the Windows 11 taskbar: click a dot to copy its hex code.*

[Français](#français) · [English](#english)

---

## Français

### Ce que ça fait

- Jusqu'à 6 ronds de couleur à côté de l'horloge.
- **Clic sur un rond** : son code (par exemple `#f5c518`) est copié dans le presse-papiers, une coche confirme la copie.
- **Clic droit** : changer de palette, gérer les palettes, quitter.
- **Quitter** : les ronds laissent la place à une seule icône palette. Un clic dessus pour choisir une palette et faire revenir les couleurs.
- **Éditeur de palettes** : un nom et jusqu'à 6 couleurs par palette. Un clic sur la pastille ouvre le sélecteur de couleurs de Windows ; le bouton pipette prend une couleur n'importe où à l'écran (loupe, flèches du clavier pour viser au pixel près, Échap pour annuler).
- Interface en français ou en anglais, au choix à l'installation.

### Prérequis

Windows 11. Rien d'autre à installer.

### Installation

1. Téléchargez `Palette-TaskBar.exe` dans la [dernière version](../../releases/latest) et lancez-le.
2. Windows peut afficher « Windows a protégé votre ordinateur », car le programme n'est pas signé numériquement. Cliquez sur **Informations complémentaires**, puis **Exécuter quand même**.
3. Choisissez votre langue : **Français** ou **English**. Le programme s'installe pour votre compte (sans droits administrateur), s'ajoute au menu Démarrer et se lancera avec Windows. Le fichier téléchargé peut ensuite être supprimé.
4. Si les ronds n'apparaissent pas à côté de l'horloge, ils sont cachés sous la flèche **^**. Pour les afficher : Paramètres → Personnalisation → Barre des tâches → Autres icônes de la barre d'état système, puis activez Palette-TaskBar.

### Utilisation

- Clic droit sur un rond → **Gérer les palettes…** pour créer vos palettes.
- Un champ vide n'affiche pas de rond.
- **Enregistrer et afficher** enregistre toutes les palettes et affiche celle qui est sélectionnée.
- **Changer de langue** : relancez le fichier téléchargé et choisissez l'autre langue, vos palettes sont conservées.
- Pour que Palette-TaskBar ne se lance plus avec Windows : Paramètres → Applications → Démarrage.
- Vos palettes sont enregistrées dans `%APPDATA%\Palette-TaskBar\palettes.json`. Vous pouvez modifier ce fichier à la main quand le programme est fermé.
- **Mise à jour** : téléchargez la nouvelle version et lancez-la, vos palettes sont conservées.

### Compiler soi-même

Tout le programme tient dans `Palette-TaskBar.cs`. Double-cliquez sur `build.bat` : il utilise le compilateur C# fourni avec Windows (.NET Framework 4), aucun outil à installer. `build.bat` ferme Palette-TaskBar s'il est ouvert, pour pouvoir remplacer l'exe.

### Désinstaller

Paramètres → Applications → Applications installées → **Palette-TaskBar** → **Désinstaller**. Le programme demande s'il faut aussi effacer vos palettes. Si l'icône reste dans la barre des tâches : clic droit dessus → **Désépingler**.

### Licence

GPL v3, voir [LICENSE](LICENSE). © 2026 Cyril Neau.

L'icône vient de [Fluent Emoji](https://github.com/microsoft/fluentui-emoji) de Microsoft (« Artist palette », style 3D), sous licence MIT, reproduite en bas de cette page.

---

## English

### What it does

- Up to 6 color dots next to the clock.
- **Click a dot**: its code (for example `#f5c518`) is copied to the clipboard, and a check mark confirms the copy.
- **Right-click**: switch palette, manage palettes, quit.
- **Quit**: the dots make way for a single palette icon. Click it to pick a palette and bring the colors back.
- **Palette editor**: a name and up to 6 colors per palette. Clicking a swatch opens the Windows color picker; the eyedropper button picks a color anywhere on screen (magnifier, arrow keys for pixel-precise aiming, Esc to cancel).
- French or English interface, chosen during installation.

### Requirements

Windows 11. Nothing else to install.

### Installation

1. Download `Palette-TaskBar.exe` from the [latest release](../../releases/latest) and run it.
2. Windows may show "Windows protected your PC" because the program is not digitally signed. Click **More info**, then **Run anyway**.
3. Choose your language: **Français** or **English**. The program installs for your account (no administrator rights), adds itself to the Start menu and will start with Windows. You can then delete the downloaded file.
4. If the dots don't appear next to the clock, they are hidden under the **^** arrow. To show them: Settings → Personalization → Taskbar → Other system tray icons, then turn Palette-TaskBar on.

### Usage

- Right-click a dot → **Manage palettes…** to create your palettes.
- An empty field shows no dot.
- **Save and show** saves all palettes and shows the selected one.
- **Changing language**: run the downloaded file again and choose the other language, your palettes are kept.
- To stop Palette-TaskBar from starting with Windows: Settings → Apps → Startup.
- Your palettes are stored in `%APPDATA%\Palette-TaskBar\palettes.json`. You can edit this file by hand while the program is closed.
- **Updating**: download the new version and run it, your palettes are kept.

### Build it yourself

The whole program is `Palette-TaskBar.cs`. Double-click `build.bat`: it uses the C# compiler that ships with Windows (.NET Framework 4), no tools to install. `build.bat` closes Palette-TaskBar if it is running, so the exe can be replaced.

### Uninstall

Settings → Apps → Installed apps → **Palette-TaskBar** → **Uninstall**. The program asks whether to also delete your palettes. If the icon stays in the taskbar: right-click it → **Unpin from taskbar**.

### License

GPL v3, see [LICENSE](LICENSE). © 2026 Cyril Neau.

The icon comes from Microsoft's [Fluent Emoji](https://github.com/microsoft/fluentui-emoji) ("Artist palette", 3D style), under the MIT license reproduced below.

---

### Fluent Emoji — MIT License

```
MIT License

Copyright (c) Microsoft Corporation.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE
```

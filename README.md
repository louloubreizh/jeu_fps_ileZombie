# Jeu Île

FPS Unity sur la carte Coast Demo de NatureManufacture, avec le contrôleur et les éléments d’entraînement de FPS Engine (Cowsins).

[Ouvrir le jeu sur GitHub Pages](https://louloubreizh.github.io/jeu_fps_ileZombie/) · [Suivre les builds](https://github.com/louloubreizh/jeu_fps_ileZombie/actions/workflows/webgl.yml)

## Unity et WebGL

Ouvrir le projet avec la version indiquée dans `ProjectSettings/ProjectVersion.txt` et le module **Web Build Support**. Après l’import des assets, **Ile → Assembler la scène** crée `Assets/Scenes/Jeu_Ile.unity`. Enregistrer les changements reproductibles dans le code d’assemblage sous `Assets/Ile/Editor`. **Ile → Construire WebGL** écrit le jeu dans `Builds/WebGL`. Servir ce dossier via HTTP pour lancer le jeu dans un navigateur.

Installer localement **FPS Engine** et le paquet principal **Coast & Dunes** avec leurs métadonnées. Le pack optionnel HDRP remplace les matériaux et shaders URP. Le runner de build retrouve les dossiers `Cowsins`, `NatureManufacture Assets`, `TextMesh Pro` et leurs fichiers `.meta` dans le cache `%LOCALAPPDATA%/IleUnity/VendorAssets` ou dans le dossier indiqué par `ISLAND_ASSETS_CACHE`.

## Publication

Le workflow **WebGL** construit et publie `main` via GitHub Actions. Il utilise le runner Windows `unity-island` avec Unity, Web Build Support et une licence active. Le deuxième job déploie l’artefact sur GitHub Pages. Le workflow peut aussi être lancé manuellement depuis **Actions**.

## Agents IA

Les trois profils de `.github/agents` couvrent la livraison WebGL, la configuration Unity et le développement du jeu. Ils se sélectionnent depuis un client GitHub Copilot compatible. GitHub Actions lance la CI/CD automatiquement à chaque push sur `main`.

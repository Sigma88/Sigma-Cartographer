# Sigma Cartographer

> Map exporting tool for Kerbal Space Program.

## USAGE

Create a folder at `GameData/Sigma/Cartographer` in your KSP folder and copy
`SigmaCartographer.dll` and `Sigma-Cartographer.version` into it. Add a
configuration file; see `CONFIGURATION` below.

The tool runs when KSP is launched, between the time all modules are loaded
and when the main menu is displayed. The generated maps will be stored in
`PluginData` in the folder containing `SigmaCartographer.dll`.

## CONFIGURATION

SigmaCartographer looks for a configuration file including the string
`@SigmaCartographer` anywhere in `GameData` or a subfolder. The file can
contain one or more sections of one or more of the types described below.
Each section contains a number of key/value pairs.

### `Map`

Generates image tiles.

If `leaflet` is `false`, the tiles are generated into `_body_/_exportFolder_/_mapType_/`.

If `leaflet` is `true`, the tiles are generated into ?

The number of tiles generated is a function of the values of `width` and `tile`:

    (width / tile) * (width / tile) / 2

| Key              | Purpose                                                                      | Default                      |
| ---------------- | ---------------------------------------------------------------------------- | ---------------------------- |
| (map type)       | see below                                                                    | `colorMap = true`            |
| `body`           | The celestial body for which to generate maps.                               | `body = Kerbin`              |
| `width`          | The total width of the map. The height will automatically be half the width. | `width = 2048`               |
| `tile`           | The width (and height) of a single tile. Should evenly divide `width`.       | `tile = 1024`                |
| `exportFolder`   | Folder name (or path) to insert into the middle of the tile path.            | `exportFolder = `            |
| `leaflet`        | If `true`, change the tile path to be compatible with Leaflet.js.            | `leaflet = false`            |
| `flipV`          | If `true`, flip each tile vertically.                                        | `flipV = false`              |
| `flipH`          | If `true`, flip each tile horizontally.                                      | `flipH = false`              |
| `alpha`          | If `true`, add alpha channel information to the generated map.               | `alpha = false`              |
| `oceanFloor`     | | `oceanFloor = true`          |
| `oceanColor`     | The color to render the ocean. A comma-separated list (R,G,B,A).             | `oceanColor = 0.1,0.1,0.2,1` |
| `LAToffset`      | The latitude of the viewpoint over the body.                                 | `LAToffset = 0`              |
| `LONoffset`      | The longitude of the viewpoint over the body.                                | `LONoffset = 0`              |
| `normalStrength` | A factor used when generating slope maps.                                    | `normalStrength = 1`         |
| `slopeMin`       | The color to use for minimum slope when generating slope maps.               | `slopeMin = 0.2,0.3,0.4,1`   |
| `slopeMax`       | The color to use for maximum slope when generating slope maps.               | `slopeMax = 0.9,0.6,0.5,1`   |
| `printTile`      | List of specific tiles to render.                                            | `printTile = `               |
| `printFrom`      | Start of the range of tiles to render.                                       | `printFrom = `               |
| `printTo`        | End of the range of tiles to render.                                         | `printTo = `                 |
| `AltitudeColor`  | List of colors used when generating maps.                                    | see below                    |

#### Map types

* `biomeMap`
* `colorMap`
* `heightMap`
* `normalMap`
* `oceanMap`
* `satelliteBiome`
* `satelliteHeight`
* `satelliteMap`
* `satelliteSlope`
* `slopeMap`

Set the desired map type(s) to `true`. Defaults to `colorMap = true` and others `false`.

#### `AltitudeColor`

Each pair in the list is of the form `relative_altitude = color`. Defaults to

```
AltitudeColor
{
  0 = 0,0,0,1
  1 = 1,1,1,1
}
```

### `Render`

Generates a spherical rendering.

The image is generated into `_body_/_exportFolder_/Render/_name_/Image.png`.

| Key               | Purpose                                                                         | Default                      |
| ----------------- | ------------------------------------------------------------------------------- | ---------------------------- |
| `texture`         | The kind of image to render.                                                    | see below                    |
| `body`            | The celestial body to render.                                                   | `body = Kerbin`              |
| `size`            | The size of the base image, which will be transformed to a spherical rendering. | `size = 2048`                |
| `exportFolder`    | Folder name (or path) to insert into the middle of the image path.              | `exportFolder = `            |
| `name`            | Subfolder name (or path) to insert into the middle of the image path.           | `name = `                    |
| `oceanFloor`      | | `oceanFloor = true`            |
| `LAToffset`       | The latitude of the viewpoint over the body.                                    | `LAToffset = 0`              |
| `LONoffset`       | The longitude of the viewpoint over the body.                                   | `LONoffset = 0`              |
| `backgroundColor` | The color used for the background of the image.                                 | `backgroundColor = 0,0,0,0 ` |
| `unlit`           | If `true`, render the image without realistic lighting.                         | `unlit = false`              |
| `_Color`          | (_Color of the scaledspace material)                                            | `_Color = 1,1,1,1`           |
| `_SpecColor`      | (_SpecColor of the scaledspace material)                                        | `_SpecColor = `              |
| `_Shininess`      | (_Shininess of the scaledspace material)                                        | `_Shininess = `              |
| `alpha`           | (adds an alpha of zero to the land parts?)                                      | `alpha = false`              |
| `oceanColor`      | The color to render the ocean. A comma-separated list (R,G,B,A).                | `oceanColor = `              |
| `normalStrength`  | A factor used when generating slope maps.                                       | `normalStrength = 1`         |
| `slopeMin`        | The color to use for minimum slope when generating slope maps.                  | `slopeMin = 0.2,0.3,0.4,1`   |
| `slopeMax`        | The color to use for maximum slope when generating slope maps.                  | `slopeMax = 0.9,0.6,0.5,1`   |
| `AltitudeColor`   | List of colors used when generating maps.                                       | see above                    |

#### `texture`

##### Pre-existing textures

* `texture = FILEPATH/[path]` - custom texture
* `texture = INTERNAL/satelliteMap`
* `texture = INTERNAL/colorMap`
* `texture = INTERNAL/biomeMap`
* `texture = INTERNAL/satelliteBiome`

##### Generate new textures

* `texture = heightMap`
* `texture = satelliteHeight`
* `texture = normalMap`
* `texture = slopeMap`
* `texture = satelliteSlope`
* `texture = colorMap`
* `texture = satelliteMap`
* `texture = oceanMap`
* `texture = biomeMap`
* `texture = satelliteBiome`

### `Info`

Generates a file at `_body_/Info.txt` with information about the body.

| Key               | Purpose                                                                   | Default            |
| ----------------- | ------------------------------------------------------------------------- | ------------------ |
| `definition`      | body is "scanned" every `definition` degrees of latitude and longitude    | `definition = 0.5` |
| `body`            | add one for each body to export; if no `body` is given, export all bodies | `body = Kerbin`    |

Example output:

```
Lowest Point
LAT = -27.07445
LON = -79.0028
ALT = -1393.53595552885

Highest Point
LAT = 61.596
LON = 46.36
ALT = 6768.58602164802

Average Elevation
Terrain = -161.206322526365
Surface = 347.469776387503

Water Coverage = 52.06%
```

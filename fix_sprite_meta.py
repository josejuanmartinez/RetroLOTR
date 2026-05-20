import os, re

BASE = r"c:\Users\jjmca\RetroLOTR"

NEW_IMAGES = [
    r"Assets\Art\Cards\Characters\LobeliaSackvilleBaggins.png",
    r"Assets\Art\Cards\Characters\WillWhitfoot.png",
    r"Assets\Art\Cards\Characters\FredegaBolger.png",
    r"Assets\Art\Cards\Characters\LothoSackvilleBaggins.png",
    r"Assets\Art\Cards\Characters\MatHeathertoes.png",
    r"Assets\Art\Cards\Actions\Events\ScouringOfTheShire.png",
    r"Assets\Art\Cards\Actions\Events\PipeweedMonopoly.png",
    r"Assets\Art\Cards\Actions\Events\LastActOfMalice.png",
    r"Assets\Art\Cards\Actions\Events\LothosPurse.png",
    r"Assets\Art\Cards\Actions\Events\GrimasKnife.png",
    r"Assets\Art\Cards\Actions\Events\TheOldMill.png",
    r"Assets\Art\Cards\Actions\Events\TheBattleOfBywater.png",
    r"Assets\Art\Cards\Actions\Events\Imprisonment.png",
    r"Assets\Art\Cards\Actions\Events\Industrialization.png",
    r"Assets\Art\Cards\PC\TheLockholes.png",
    r"Assets\Art\Cards\PC\Bywater.png",
    r"Assets\Art\Cards\Armies\RuffiansLightInfantry.png",
]

META_TEMPLATE = """\
fileFormatVersion: 2
guid: {guid}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 13
  mipmaps:
    mipMapMode: 0
    enableMipMap: 0
    sRGBTexture: 1
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 0
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: 2048
  textureSettings:
    serializedVersion: 2
    filterMode: 1
    aniso: 1
    mipBias: 0
    wrapU: 1
    wrapV: 1
    wrapW: 1
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 50
  spriteMode: 1
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: 1
  alphaIsTransparency: 1
  spriteTessellationDetail: -1
  textureType: 8
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  maxTextureSizeSet: 0
  compressionQualitySet: 0
  textureFormatSet: 0
  ignorePngGamma: 0
  applyGammaDecoding: 0
  swizzle: 50462976
  cookieLightType: 0
  platformSettings:
  - serializedVersion: 4
    buildTarget: DefaultTexturePlatform
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: WebGL
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Standalone
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 4
    buildTarget: Server
    maxTextureSize: 2048
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 50
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  spriteSheet:
    serializedVersion: 2
    sprites: []
    outline: []
    customData:
    physicsShape: []
    bones: []
    spriteID: 5e97eb03825dee720800000000000000
    internalID: 0
    vertices: []
    indices:
    edges: []
    weights: []
    secondaryTextures: []
    spriteCustomMetadata:
      entries: []
    nameFileIdTable: {{}}
  mipmapLimitGroupName:
  pSDRemoveMatte: 0
  userData:
  assetBundleName:
  assetBundleVariant:
"""

fixed = 0
for rel_path in NEW_IMAGES:
    meta_path = os.path.join(BASE, rel_path + ".meta")
    if not os.path.exists(meta_path):
        print(f"MISSING meta: {meta_path}")
        continue
    with open(meta_path, "r", encoding="utf-8") as f:
        content = f.read()
    m = re.search(r"guid: ([a-f0-9]+)", content)
    if not m:
        print(f"No GUID found in: {meta_path}")
        continue
    guid = m.group(1)
    new_content = META_TEMPLATE.format(guid=guid)
    with open(meta_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(new_content)
    print(f"Fixed: {rel_path} (guid={guid})")
    fixed += 1

print(f"\nDone. Fixed {fixed}/{len(NEW_IMAGES)} meta files.")

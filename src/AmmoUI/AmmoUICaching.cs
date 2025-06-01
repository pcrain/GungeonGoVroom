namespace GGV;

[HarmonyPatch]
internal static class AmmoUICaching
{
  private static bool Prepare(MethodBase original)
  {
    return false;
    if (!GGVConfig.OPT_AMMO_DISPLAY)
      return false;
    if (original == null)
      GGVDebug.LogPatch($"Patching class {MethodBase.GetCurrentMethod().DeclaringType}");
    else
      GGVDebug.LogPatch($"  Patching {original.DeclaringType}.{original.Name}");
    return true;
  }

  /// <summary>Class for holding various cached metadata for rendering ammo ui sprites</summary>
  private class QuickInvalidateData
  {
    private static readonly Dictionary<GameObject, List<QuickInvalidateData>> _CachedQIDs = new();
    private static readonly Dictionary<dfTiledSprite, QuickInvalidateData> _QIDForSprite = new();

    public bool inUse = false; // whether this particular sprite is actively in use by an ammo bar
    public GameObject spritePrefab = null; // the ammo ui sprite prefab we've instantiated
    public GameObject spriteObject = null; // the ammo ui sprite GameObject instance
    public dfTiledSprite sprite = null; // the sprite for the GameObject instance
    public dfRenderData preservedData = null; // the dfRenderData preserved during floor transitions
    public int maxNumTiles = 0;  // the maximum number of tiles this sprite has ever rendered

    public static QuickInvalidateData ForSprite(dfTiledSprite sprite) => _QIDForSprite[sprite];

    public static void PreserveRenderData()
    {
      foreach (var kvp in _QIDForSprite)
      {
        if (kvp.Key is not dfTiledSprite sprite || sprite.renderData == null)
          continue;

        // GGVDebug.Log($"    preserving render data for {sprite.name} == {sprite.spriteName} == {sprite.SpriteInfo.name}");
        QuickInvalidateData qid = kvp.Value;
        qid.preservedData = sprite.renderData;
        qid.inUse = false;
        sprite.renderData = null;
      }
    }

    private static QuickInvalidateData RequestRaw(GameObject prefab)
    {
      if (!_CachedQIDs.TryGetValue(prefab, out List<QuickInvalidateData> qidList))
        _CachedQIDs[prefab] = qidList = new();

      for (int i = qidList.Count - 1; i >= 0; --i)
      {
        if (qidList[i].inUse)
          continue;
        qidList[i].inUse = true;
        return qidList[i];
      }

      GameObject newSpriteInstance = UnityEngine.Object.Instantiate(prefab);
      QuickInvalidateData newQid = new(){
        inUse         = true,
        spritePrefab  = prefab,
        spriteObject  = newSpriteInstance,
        sprite        = newSpriteInstance.GetComponent<dfTiledSprite>(),
        preservedData = null,
        maxNumTiles   = 0,
      };
      _QIDForSprite[newQid.sprite] = newQid;
      qidList.Add(newQid);

      // GGVDebug.Log($"getting new render data for {newQid.sprite.spriteName} with {qidList.Count} cached elements");
      return newQid;
    }

    public static GameObject Request(GameObject prefab)
    {
      QuickInvalidateData qid = RequestRaw(prefab);
      if (qid.spriteObject)
      {
        qid.inUse = true;
        qid.spriteObject.SetActive(true);
        return qid.spriteObject;
      }

      if (!_QIDForSprite.ContainsKey(qid.sprite))
        throw new InvalidOperationException("something went horribly wrong caching ammo sprites, part 1 D:");
      if (qid.preservedData == null)
        throw new InvalidOperationException("something went horribly wrong caching ammo sprites, part 2 D:");

      _QIDForSprite.Remove(qid.sprite); // remove the destroyed sprite instance from our lookup table
      qid.inUse = true;
      qid.maxNumTiles = qid.preservedData.Vertices.Count / 4;
      qid.spriteObject = UnityEngine.Object.Instantiate(prefab);
      qid.sprite = qid.spriteObject.GetComponent<dfTiledSprite>();
      qid.sprite.renderData = qid.preservedData;
      qid.preservedData = null;
      _QIDForSprite[qid.sprite] = qid; // add the new sprite instance to our lookup table
      // GGVDebug.Log($"restored ammo render data with size {qid.maxNumTiles}");
      return qid.spriteObject;
    }

    public static void ReturnSprite(dfTiledSprite sprite)
    {
      if (sprite.controls != null)
      {
        //NOTE: need to destroy AmmoBurstVFX playing on the control
        for (int ci = sprite.controls.Count - 1; ci >= 0; --ci)
          if (sprite.controls[ci] is dfControl dfc)
            UnityEngine.Object.Destroy(dfc.gameObject);
        sprite.controls.Clear();
      }

      QuickInvalidateData qid = _QIDForSprite[sprite];
      qid.inUse = false;
      sprite.gameObject.SetActive(false);
      sprite.parent.RemoveControl(sprite);
    }
  }

  /// <summary>Stashes away dfRenderData for ammo clip sprites so it can be reused next floor load.</summary>
  [HarmonyPatch(typeof(GameUIAmmoController), nameof(GameUIAmmoController.OnDestroy))]
  [HarmonyPrefix]
  private static void GameUIAmmoControllerOnDestroyPatch(GameUIAmmoController __instance)
  {
    QuickInvalidateData.PreserveRenderData();
  }

  [HarmonyPatch(typeof(GameUIAmmoController), nameof(GameUIAmmoController.UpdateAmmoUIForModule))]
  [HarmonyILManipulator]
  private static void GameUIAmmoControllerUpdateAmmoUIForModulePatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      ILLabel afterUIAmmoChangeLabel = null;
      if (!cursor.TryGotoNext(MoveType.Before,
          instr => instr.MatchLdarg(10), // cachedCustomAmmoTypeForModule
          instr => instr.MatchLdindRef(),
          instr => instr.MatchCall<string>("op_Inequality"),
          instr => instr.MatchBrfalse(out afterUIAmmoChangeLabel)))
          return;

      if (!cursor.TryGotoNext(MoveType.AfterLabel,
          instr => instr.MatchLdarg(0),
          instr => instr.MatchLdfld<GameUIAmmoController>("m_additionalAmmoTypeDefinitions")))
          return;

      for (int i = 0; i <= 13; ++i) // forward all arguments
        cursor.Emit(OpCodes.Ldarg, i);
      cursor.CallPrivate(typeof(AmmoUICaching), nameof(FastUpdateAmmoUIForModule));
      cursor.Emit(OpCodes.Brfalse, afterUIAmmoChangeLabel);
  }

  /// <summary>Prevent CleanupLists() from removing our ammo sprites</summary>
  [HarmonyPatch(typeof(GameUIAmmoController), nameof(GameUIAmmoController.CleanupLists))]
  [HarmonyILManipulator]
  private static void GameUIAmmoControllerCleanupListsPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      //NOTE: the first four calls to Destroy() are for the ammo sprites we're preserving, the last two are not
      for (int i = 0; i < 4; ++i)
      {
        if (!cursor.TryGotoNext(MoveType.Before,
            instr => instr.MatchCallvirt<UnityEngine.Component>("get_gameObject"),
            instr => instr.MatchCall<UnityEngine.Object>(nameof(UnityEngine.Object.Destroy))))
            return;
        cursor.RemoveRange(2); // keep the dfTiledSprite on the stack and don't destroy its object
        cursor.CallPublic(typeof(QuickInvalidateData), nameof(QuickInvalidateData.ReturnSprite));
      }
  }

  [HarmonyPatch(typeof(dfControl), nameof(dfControl.Render))]
  [HarmonyILManipulator]
  private static void dfControlRenderPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      ILLabel notInvalidatedLabel = null;
      if (!cursor.TryGotoNext(MoveType.After,
          instr => instr.MatchLdfld<dfControl>("isControlInvalidated"),
          instr => instr.MatchBrfalse(out notInvalidatedLabel)))
          return;

      cursor.Emit(OpCodes.Ldarg_0);
      cursor.CallPrivate(typeof(AmmoUICaching), nameof(AttemptQuickInvalidate));
      cursor.Emit(OpCodes.Brfalse, notInvalidatedLabel);
  }

  /// <summary>Return false to skip original invalidation logic</summary>
  private static bool AttemptQuickInvalidate(dfControl control)
  {
    if (control is not dfTiledSprite tile || tile.Atlas == null || tile.SpriteInfo == null)
      return true; // call original invalidation logic
    if (QuickInvalidateData.ForSprite(tile) is not QuickInvalidateData qid)
      return true; // call original invalidation logic

    if (qid.maxNumTiles == 0) // clear out renderData only the very first time this is called, update it in place otherwise
    {
      tile.renderData.Clear();
      tile.renderData.Material = tile.Atlas.Material;
    }

    dfList<Vector3> vertices = tile.renderData.Vertices;
    dfList<Vector2> uV = tile.renderData.UV;
    dfList<Color32> colors = tile.renderData.Colors;
    dfList<int> triangles = tile.renderData.Triangles;
    Vector2[] spriteUV = tile.buildQuadUV();
    Vector2 spriteSize = Vector2.Scale(tile.SpriteInfo.sizeInPixels, tile.tileScale);
    Vector2 scrollFraction = new Vector2(tile.tileScroll.x % 1f, tile.tileScroll.y % 1f);
    float blackLineAdjustment = ((!tile.EnableBlackLineFix) ? 0f : (-0.1f));
    int tilesNeeded = (int)((tile.size.y + Mathf.Abs(scrollFraction.y * spriteSize.y)) / spriteSize.y);
    if (tilesNeeded > qid.maxNumTiles) // pre-allocate space in our lists for all of the data we need
    {
      vertices.EnsureCapacity(4 * tilesNeeded);
      triangles.EnsureCapacity(6 * tilesNeeded);
      uV.EnsureCapacity(4 * tilesNeeded);
      colors.EnsureCapacity(4 * tilesNeeded);
    }

    Color32 baseColor = tile.ApplyOpacity(tile.isEnabled ? tile.color : tile.disabledColor);
    int tileIndex = 0;
    for (float y = 0f - Mathf.Abs(scrollFraction.y * spriteSize.y); y < tile.size.y; y += spriteSize.y)
    {
      for (float x = 0f - Mathf.Abs(scrollFraction.x * spriteSize.x); x < tile.size.x; x += spriteSize.x)
      {
        if (tileIndex >= qid.maxNumTiles)
        {
          int count = vertices.Count;
          vertices.Add(new Vector3(x, 0f - y));
          vertices.Add(new Vector3(x + spriteSize.x, 0f - y));
          vertices.Add(new Vector3(x + spriteSize.x, 0f - y + (0f - spriteSize.y) + blackLineAdjustment));
          vertices.Add(new Vector3(x, 0f - y + (0f - spriteSize.y) + blackLineAdjustment));
          tile.addQuadTriangles(triangles, count);
          tile.addQuadUV(uV, spriteUV);
          tile.addQuadColors(colors);
        }
        else
        {
          int offset = 4 * tileIndex;
          vertices.items[offset + 0].x = x;
          vertices.items[offset + 0].y = -y;
          vertices.items[offset + 1].x = x + spriteSize.x;
          vertices.items[offset + 1].y = -y;
          vertices.items[offset + 2].x = x + spriteSize.x;
          vertices.items[offset + 2].y = -y - spriteSize.y + blackLineAdjustment;
          vertices.items[offset + 3].x = x;
          vertices.items[offset + 3].y = -y - spriteSize.y + blackLineAdjustment;
          colors.items  [offset + 0] = baseColor;
          colors.items  [offset + 1] = baseColor;
          colors.items  [offset + 2] = baseColor;
          colors.items  [offset + 3] = baseColor;
        }
        ++tileIndex;
      }
    }

    // update colors only as needed
    Color32 disabledColor = tile.ApplyOpacity(tile.disabledColor);
    int lastColorIndex = 4 * qid.maxNumTiles;
    for (int i = 4 * tileIndex; i < lastColorIndex; ++i)
      colors.items[i] = disabledColor;

    //NOTE: clipping is unnecessary for ammo clip sprites, so we can skip this step
    // tile.clipQuads(vertices, uV);

    // adjust vertices to sprite pivot
    float pixelsToUnits = tile.PixelsToUnits();
    Vector3 vector3 = tile.pivot.TransformToUpperLeft(tile.size);
    for (int i = 0; i < vertices.Count; i++)
      vertices[i] = (vertices[i] + vector3) * pixelsToUnits;

    //NOTE: ammo sprites aren't interactive so we can skip this step as well
    // tile.updateCollider();

    #if DEBUG
    if (tilesNeeded > qid.maxNumTiles)
      GGVDebug.Log($"updated {tileIndex} ammo render tiles (new: {(int)tileIndex - qid.maxNumTiles})");
    #endif

    if (tilesNeeded > qid.maxNumTiles)
      qid.maxNumTiles = tilesNeeded;

    return false; // skip the original invalidation logic
  }

  /// <summary>Returns false to skip vanilla ammo rebuilding, or true to use our own</summary>
  private static bool FastUpdateAmmoUIForModule(GameUIAmmoController self, ref dfTiledSprite currentAmmoFGSprite, ref dfTiledSprite currentAmmoBGSprite, List<dfTiledSprite> AddlModuleFGSprites, List<dfTiledSprite> AddlModuleBGSprites, dfSprite ModuleTopCap, dfSprite ModuleBottomCap, ProjectileModule module, Gun currentGun, ref GameUIAmmoType.AmmoType cachedAmmoTypeForModule, ref string cachedCustomAmmoTypeForModule, ref int cachedShotsInClip, bool didChangeGun, int numberRemaining)
  {
    if (currentGun.CurrentOwner is not PlayerController player)
      return true; // call original code
    int pid = player.PlayerIDX;
    if (pid != 0 && pid != 1)
      return true; // call original code

    self.m_additionalAmmoTypeDefinitions.Clear();
    if (currentAmmoFGSprite != null)
      QuickInvalidateData.ReturnSprite(currentAmmoFGSprite);
    if (currentAmmoBGSprite != null)
      QuickInvalidateData.ReturnSprite(currentAmmoBGSprite);
    for (int i = 0; i < AddlModuleBGSprites.Count; i++)
    {
      QuickInvalidateData.ReturnSprite(AddlModuleBGSprites[i]);
      QuickInvalidateData.ReturnSprite(AddlModuleFGSprites[i]);
    }

    AddlModuleBGSprites.Clear();
    AddlModuleFGSprites.Clear();
    GameUIAmmoType uIAmmoType = self.GetUIAmmoType(module.ammoType, module.customAmmoType);
    GameObject newFg = QuickInvalidateData.Request(uIAmmoType.ammoBarFG.gameObject);
    GameObject newBg = QuickInvalidateData.Request(uIAmmoType.ammoBarBG.gameObject);
    newFg.transform.parent = self.GunBoxSprite.transform.parent;
    newBg.transform.parent = self.GunBoxSprite.transform.parent;
    newFg.name = uIAmmoType.ammoBarFG.name;
    newBg.name = uIAmmoType.ammoBarBG.name;
    currentAmmoFGSprite = newFg.GetComponent<dfTiledSprite>();
    currentAmmoBGSprite = newBg.GetComponent<dfTiledSprite>();
    self.m_panel.AddControl(currentAmmoFGSprite);
    self.m_panel.AddControl(currentAmmoBGSprite);
    currentAmmoFGSprite.EnableBlackLineFix = module.shootStyle == ProjectileModule.ShootStyle.Beam;
    currentAmmoBGSprite.EnableBlackLineFix = currentAmmoFGSprite.EnableBlackLineFix;

    if (module.usesOptionalFinalProjectile)
    {
      GameUIAmmoType uIAmmoType2 = self.GetUIAmmoType(module.finalAmmoType, module.finalCustomAmmoType);
      self.m_additionalAmmoTypeDefinitions.Add(uIAmmoType2);
      newFg = QuickInvalidateData.Request(uIAmmoType2.ammoBarFG.gameObject);
      newBg = QuickInvalidateData.Request(uIAmmoType2.ammoBarBG.gameObject);
      newFg.transform.parent = self.GunBoxSprite.transform.parent;
      newBg.transform.parent = self.GunBoxSprite.transform.parent;
      newFg.name = uIAmmoType2.ammoBarFG.name;
      newBg.name = uIAmmoType2.ammoBarBG.name;
      AddlModuleFGSprites.Add(newFg.GetComponent<dfTiledSprite>());
      AddlModuleBGSprites.Add(newBg.GetComponent<dfTiledSprite>());
      self.m_panel.AddControl(AddlModuleFGSprites[0]);
      self.m_panel.AddControl(AddlModuleBGSprites[0]);
    }

    return false; // skip remainder of original ammo swapping code
  }
}

#if DEBUG
[HarmonyPatch]
internal static class DebugAmmoUIRAMTracker
{
  private static long _totalBytes = 0;

  [HarmonyPatch(typeof(dfTiledSprite), nameof(dfTiledSprite.OnRebuildRenderData))]
  [HarmonyPostfix]
  private static void dfTiledSpriteOnRebuildRenderDataPatch(dfTiledSprite __instance)
  {
    var d = __instance.renderData;
    var bytes = 12 * d.Vertices.Count + 16 * d.Colors.count + 8 * d.UV.Count + 4 * d.Triangles.Count;
    _totalBytes += bytes;
    GGVDebug.Log($"rebuilt {bytes} bytes ({_totalBytes:n0} total) of {__instance.SpriteName} render data for {d.Vertices.Count} vertices, {d.Colors.count} colors, {d.UV.Count} uvs, and {d.Triangles.Count} triangles");
  }
}
#endif

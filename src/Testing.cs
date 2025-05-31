namespace GGV;

#if DEBUG

[HarmonyPatch]
internal static class PatchesUnderTesting
{
  private static long totalBytes = 0;
  [HarmonyPatch(typeof(dfTiledSprite), nameof(dfTiledSprite.OnRebuildRenderData))]
  [HarmonyPostfix]
  private static void dfTiledSpriteOnRebuildRenderDataPatch(dfTiledSprite __instance)
  {
    var d = __instance.renderData;
    var bytes = 12 * d.Vertices.Count + 16 * d.Colors.count + 8 * d.UV.Count + 4 * d.Triangles.Count;
    totalBytes += bytes;
    GGVDebug.Log($"rebuilt {bytes} bytes ({totalBytes:n0} total) of {__instance.SpriteName} render data for {d.Vertices.Count} vertices, {d.Colors.count} colors, {d.UV.Count} uvs, and {d.Triangles.Count} triangles");
  }

  /*
  private void UpdateAmmoUIForModule(
    1   ref dfTiledSprite currentAmmoFGSprite,
    2   ref dfTiledSprite currentAmmoBGSprite,
    3   List<dfTiledSprite> AddlModuleFGSprites,
    4   List<dfTiledSprite> AddlModuleBGSprites,
    5   dfSprite ModuleTopCap,
    6   dfSprite ModuleBottomCap,
    7   ProjectileModule module,
    8   Gun currentGun,
    9   ref GameUIAmmoType.AmmoType cachedAmmoTypeForModule,
    10  ref string cachedCustomAmmoTypeForModule,
    11  ref int cachedShotsInClip,
    12  bool didChangeGun,
    13  int numberRemaining
    )
  */

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

      cursor.Emit(OpCodes.Ldarg, 0);
      cursor.Emit(OpCodes.Ldarg, 1);
      // cursor.Emit(OpCodes.Ldind_Ref);
      cursor.Emit(OpCodes.Ldarg, 2);
      // cursor.Emit(OpCodes.Ldind_Ref);
      cursor.Emit(OpCodes.Ldarg, 3);
      cursor.Emit(OpCodes.Ldarg, 4);
      cursor.Emit(OpCodes.Ldarg, 5);
      cursor.Emit(OpCodes.Ldarg, 6);
      cursor.Emit(OpCodes.Ldarg, 7);
      cursor.Emit(OpCodes.Ldarg, 8);
      cursor.Emit(OpCodes.Ldarg, 9);
      // cursor.Emit(OpCodes.Ldind_Ref);
      cursor.Emit(OpCodes.Ldarg, 10);
      // cursor.Emit(OpCodes.Ldind_Ref);
      cursor.Emit(OpCodes.Ldarg, 11);
      // cursor.Emit(OpCodes.Ldind_Ref);
      cursor.Emit(OpCodes.Ldarg, 12);
      cursor.Emit(OpCodes.Ldarg, 13);
      cursor.CallPrivate(typeof(PatchesUnderTesting), nameof(AmmoTest));
      cursor.Emit(OpCodes.Brfalse, afterUIAmmoChangeLabel);

      return;
  }

  [HarmonyPatch(typeof(dfControl), nameof(dfControl.Render))]
  [HarmonyILManipulator]
  private static void dfControlRenderPatchIL(ILContext il)
  {
      ILCursor cursor = new ILCursor(il);
      ILLabel notInvalidatedLabel = null;
      if (!cursor.TryGotoNext(MoveType.After,
          instr => instr.MatchLdarg(0),
          instr => instr.MatchLdfld<dfControl>("isControlInvalidated"),
          instr => instr.MatchBrfalse(out notInvalidatedLabel)))
          return;

      cursor.Emit(OpCodes.Ldarg_0);
      cursor.CallPrivate(typeof(PatchesUnderTesting), nameof(AttemptQuickInvalidate));
      cursor.Emit(OpCodes.Brfalse, notInvalidatedLabel);

      return;
  }

  /// <summary>Return false to skip original invalidation logic</summary>
  private static bool AttemptQuickInvalidate(dfControl control)
  {
    if (control is not dfTiledSprite tile)
      return true; // call original invalidation logic
    if (!_QuickInvalidateData.TryGetValue(tile, out QuickInvalidateData qid))
      return true; // call original invalidation logic
    if (tile.Atlas == null)
      return true;
    if (tile.SpriteInfo == null)
      return true;

    if (qid.maxNumTiles == 0)
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

    // update colors as needed
    Color32 disabledColor = tile.ApplyOpacity(tile.disabledColor);
    for (int i = 4 * tileIndex; i < 4 * qid.maxNumTiles; ++i)
      colors.items[i] = disabledColor;

    //NOTE: clipping is unnecessary for ammo clip sprites, so we can skip this step
    // tile.clipQuads(vertices, uV);

    float pixelsToUnits = tile.PixelsToUnits();
    Vector3 vector3 = tile.pivot.TransformToUpperLeft(tile.size);
    for (int i = 0; i < vertices.Count; i++)
      vertices[i] = (vertices[i] + vector3) * pixelsToUnits;

    //NOTE: ammo sprites aren't interactive so we can skip this step as well
    // tile.updateCollider();

    if (tileIndex > qid.maxNumTiles)
      GGVDebug.Log($"updated {tileIndex} tiles (new: {Mathf.Max(0, (int)tileIndex - qid.maxNumTiles)})");
    qid.lastNumTiles = tilesNeeded;
    if (tilesNeeded > qid.maxNumTiles)
      qid.maxNumTiles = tilesNeeded;

    return false; // skip the original invalidation logic
  }

  private static readonly Dictionary<GameObject, GameObject>[] _CachedAmmoBars = [new(), new()];
  private static readonly Dictionary<dfTiledSprite, QuickInvalidateData> _QuickInvalidateData = new();

  private class QuickInvalidateData
  {
    public int maxNumTiles = 0;  // the maximum number of tiles this sprite has ever rendered
    public int lastNumTiles = 0; // the previous number of tiles this sprite has ever rendered
  }

  private static GameObject GetCachedAmmoSprite(GameObject prefab, Dictionary<GameObject, GameObject> cachedAmmo)
  {
    //NOTE: DontDestroyOnLoad() doesn't seem to work for ammo sprites and I don't feel like debugging why, so we re-cache every floor
    if (cachedAmmo.TryGetValue(prefab, out GameObject cachedInstance) && cachedInstance)
    {
      cachedInstance.SetActive(true);
      return cachedInstance;
    }
    GameObject newG = cachedAmmo[prefab] = UnityEngine.Object.Instantiate(prefab);
    _QuickInvalidateData[newG.GetComponent<dfTiledSprite>()] = new();
    return newG;
  }

  /// <summary>Returns false to skip vanilla ammo rebuilding, or true to use our own</summary>
  private static bool AmmoTest(GameUIAmmoController self, ref dfTiledSprite currentAmmoFGSprite, ref dfTiledSprite currentAmmoBGSprite, List<dfTiledSprite> AddlModuleFGSprites, List<dfTiledSprite> AddlModuleBGSprites, dfSprite ModuleTopCap, dfSprite ModuleBottomCap, ProjectileModule module, Gun currentGun, ref GameUIAmmoType.AmmoType cachedAmmoTypeForModule, ref string cachedCustomAmmoTypeForModule, ref int cachedShotsInClip, bool didChangeGun, int numberRemaining)
  {
    if (currentGun.CurrentOwner is not PlayerController player)
      return true; // call original code
    int pid = player.PlayerIDX;
    if (pid != 0 && pid != 1)
      return true; // call original code

    Dictionary<GameObject, GameObject> cachedAmmo = _CachedAmmoBars[pid];

    self.m_additionalAmmoTypeDefinitions.Clear();
    if (currentAmmoFGSprite != null)
    {
      //NOTE: need to destroy AmmoBurstVFX playing on the control
      if (currentAmmoFGSprite.controls != null)
      {
        for (int ci = currentAmmoFGSprite.controls.Count - 1; ci >= 0; --ci)
          UnityEngine.Object.Destroy(currentAmmoFGSprite.controls[ci]);
        currentAmmoFGSprite.controls.Clear();
      }
      currentAmmoFGSprite.gameObject.SetActive(false);
      self.m_panel.RemoveControl(currentAmmoFGSprite);
      // UnityEngine.Object.Destroy(currentAmmoFGSprite.gameObject);
    }
    if (currentAmmoBGSprite != null)
    {
      currentAmmoBGSprite.gameObject.SetActive(false);
      self.m_panel.RemoveControl(currentAmmoBGSprite);
      // UnityEngine.Object.Destroy(currentAmmoBGSprite.gameObject);
    }
    for (int i = 0; i < AddlModuleBGSprites.Count; i++)
    {
      AddlModuleBGSprites[i].gameObject.SetActive(false);
      self.m_panel.RemoveControl(AddlModuleBGSprites[i]);
      // UnityEngine.Object.Destroy(AddlModuleBGSprites[i].gameObject);
      AddlModuleFGSprites[i].gameObject.SetActive(false);
      self.m_panel.RemoveControl(AddlModuleFGSprites[i]);
      // UnityEngine.Object.Destroy(AddlModuleFGSprites[i].gameObject);
    }
    AddlModuleBGSprites.Clear();
    AddlModuleFGSprites.Clear();
    GameUIAmmoType uIAmmoType = self.GetUIAmmoType(module.ammoType, module.customAmmoType);
    GameObject newFg = GetCachedAmmoSprite(uIAmmoType.ammoBarFG.gameObject, cachedAmmo);
    // GameObject newFg = UnityEngine.Object.Instantiate(uIAmmoType.ammoBarFG.gameObject);
    GameObject newBg = GetCachedAmmoSprite(uIAmmoType.ammoBarBG.gameObject, cachedAmmo);
    // GameObject newBg = UnityEngine.Object.Instantiate(uIAmmoType.ammoBarBG.gameObject);
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
      newFg = GetCachedAmmoSprite(uIAmmoType2.ammoBarFG.gameObject, cachedAmmo);
      // newFg = UnityEngine.Object.Instantiate(uIAmmoType2.ammoBarFG.gameObject);
      newBg = GetCachedAmmoSprite(uIAmmoType2.ammoBarBG.gameObject, cachedAmmo);
      // newBg = UnityEngine.Object.Instantiate(uIAmmoType2.ammoBarBG.gameObject);
      newFg.transform.parent = self.GunBoxSprite.transform.parent;
      newBg.transform.parent = self.GunBoxSprite.transform.parent;
      newFg.name = uIAmmoType2.ammoBarFG.name;
      newBg.name = uIAmmoType2.ammoBarBG.name;
      AddlModuleFGSprites.Add(newFg.GetComponent<dfTiledSprite>());
      AddlModuleBGSprites.Add(newBg.GetComponent<dfTiledSprite>());
      self.m_panel.AddControl(AddlModuleFGSprites[0]);
      self.m_panel.AddControl(AddlModuleBGSprites[0]);
    }
    return false;
  }
}

#endif

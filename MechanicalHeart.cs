using Microsoft.Xna.Framework;
using NetSimplified;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Config;
using Terraria.ModLoader.IO;
namespace MechanicalHeart;

// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
public class MechanicalHeart : Mod
{
    public override void Load()
    {
        NetModuleLoader.CurrentMod = this;
        NetModuleLoader.LoadAutoSyncsFrom(typeof(NetModuleLoader).Assembly);
        NetModuleLoader.LoadAutoSyncsFrom(Assembly.GetExecutingAssembly());
        NetModuleLoader.LoadNetModules();
    }
    public override object Call(params object[] args)
    {
        object netReply = CrossMod.HandleModCalls(args);
        if (netReply is not false)
        {
            return netReply;
        }

        return base.Call(args);
    }

    public override void HandlePacket(BinaryReader reader, int whoAmI)
    {
        NetModule.ReceiveModule(reader, whoAmI);
    }
}
public class MechanicalHeartItem : ModItem
{
    public override void SetStaticDefaults()
    {
        Item.ResearchUnlockCount = 3;
    }
    //public static int HeartMax => ModContent.GetInstance<MHConfig>().UseFreeMode ? ModContent.GetInstance<MHConfig>().Parameter : ModContent.GetInstance<BossDefeatedCountSystem>().BossDefeatedCount / ModContent.GetInstance<MHConfig>().Parameter;
    public static int HeartMax
    {
        get
        {
            var config = ModContent.GetInstance<MHConfig>();
            if (config.UseFreeMode)
                return config.Parameter;
            int count = ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList.Count;
            return count / config.Parameter;
        }
    }

    public override void SetDefaults()
    {
        Item.CloneDefaults(ItemID.LifeFruit);
    }
    public override bool? UseItem(Player player)
    {
        if (player.GetModPlayer<MechanicalHeartPlayer>().MechanicalHeartCount >= HeartMax)
        {
            return null;
        }
        player.GetModPlayer<MechanicalHeartPlayer>().MechanicalHeartCount++;
        return true;
    }
    public override void AddRecipes()
    {
        CreateRecipe().AddIngredient(ItemID.DemonHeart).AddIngredient(ItemID.SoulofMight, 10).AddIngredient(ItemID.SoulofFright, 10).AddIngredient(ItemID.SoulofSight, 10).AddTile(TileID.MythrilAnvil).Register();
        CreateRecipe().AddIngredient(ItemID.GuideVoodooDoll).AddIngredient(ItemID.SoulofLight, 15).AddIngredient(ItemID.SoulofNight, 15).AddIngredient(ItemID.SoulofMight, 10).AddIngredient(ItemID.SoulofFright, 10).AddIngredient(ItemID.SoulofSight, 10).AddTile(TileID.MythrilAnvil).Register();
        base.AddRecipes();
    }
}
public class MechanicalHeartPlayer : ModPlayer
{
    public int MechanicalHeartCount;
    public bool synced = false;
    public override void SaveData(TagCompound tag)
    {
        tag[nameof(MechanicalHeartCount)] = MechanicalHeartCount;
    }

    public override void LoadData(TagCompound tag)
    {
        MechanicalHeartCount = tag.GetInt(nameof(MechanicalHeartCount));
        synced = false;
    }

    public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
    {
        if (!ModContent.GetInstance<MHConfig>().ShowList) return;
        var list = ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList;
        Main.spriteBatch.DrawString(FontAssets.MouseText.Value, list.Count.ToString(), Player.Center - Main.screenPosition + new Vector2(0, -64), Color.Lerp(Main.DiscoColor, Color.White, .5f));
        float yoff = 0;
        foreach (var str in list)
        {
            Main.spriteBatch.DrawString(FontAssets.MouseText.Value, str, Player.Center - Main.screenPosition + new Vector2(32, -64 + yoff), Color.White);

            yoff += FontAssets.MouseText.Value.MeasureString(str).Y;
        }
        base.ModifyDrawInfo(ref drawInfo);
    }
    public override void ResetEffects()
    {
        if (!synced)
        {
            synced = true;
            if (Main.dedServ)
            {
                SyncBossDefList.Get().Send(Player.whoAmI);
            }
        }
        base.ResetEffects();
    }
}
public class BossDefeatedCountSystem : ModSystem
{
    public List<string> defeatedBossList = new();
    public override void LoadWorldData(TagCompound tag)
    {
        defeatedBossList = tag.Get<List<string>>(nameof(defeatedBossList));
        base.LoadWorldData(tag);
    }
    public override void SaveWorldData(TagCompound tag)
    {
        tag.Add(nameof(defeatedBossList), defeatedBossList);
        base.SaveWorldData(tag);
    }
}
public class SyncBossDefList : NetModule
{
    string[] myList;
    public static SyncBossDefList Get() => Get(ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList.ToArray());

    public static SyncBossDefList Get(List<string> list) => Get(list.ToArray());
    public static SyncBossDefList Get(string[] list)
    {
        var result = NetModuleLoader.Get<SyncBossDefList>();
        result.myList = list;
        return result;
    }
    public override void Send(ModPacket p)
    {
        p.Write(myList.Length);
        for (int n = 0; n < myList.Length; n++)
            p.Write(myList[n]);
        base.Send(p);
    }
    public override void Read(BinaryReader r)
    {
        int l = r.ReadInt32();
        myList = new string[l];
        for (int i = 0; i < l; i++)
            myList[i] = r.ReadString();
        base.Read(r);
    }
    public override void Receive()
    {
        ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList = myList.ToList();
    }
}
public class MHConfig : ModConfig
{
    [DefaultValue(false)]
    public bool UseFreeMode = false;

    [DefaultValue(5)]
    [Range(1, 10)]
    public int Parameter;

    [DefaultValue(false)]
    public bool CustomPos = false;
    public override ConfigScope Mode => ConfigScope.ServerSide;

    [DefaultValue(false)]
    public bool ShowList = false;
}
public class MHGlobalNPC : GlobalNPC
{
    public override void OnKill(NPC npc)
    {
        var key = $"{npc?.ModNPC?.Mod?.Name ?? "Terraria"}/{NPCID.Search.GetName(npc.type)}";
        var list = ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList;
        if ((npc.boss || npc.type == NPCID.EaterofWorldsHead) && !npc.dontCountMe && !list.Contains(key))
        {
            ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList.Add(key);
            if (Main.dedServ)
            {
                SyncBossDefList.Get(ModContent.GetInstance<BossDefeatedCountSystem>().defeatedBossList).Send();
            }
        }
        base.OnKill(npc);
    }

}
public abstract class MechanicalAccSlotBase : ModAccessorySlot
{
    protected abstract int Index { get; }
    public override Vector2? CustomLocation => ModContent.GetInstance<MHConfig>().CustomPos ? new Vector2((1 - Index / 5) * 160, Index % 5 * 50) + Main.ScreenSize.ToVector2() * new Vector2(0.75f - 0.25f * (Main.UIScale - 1), .5f) : null;
    public override string FunctionalBackgroundTexture => "MechanicalHeart/Inventory_BackMH";
    public override bool CanAcceptItem(Item checkItem, AccessorySlotType context)
    {
        int count = Main.LocalPlayer.GetModPlayer<MechanicalHeartPlayer>().MechanicalHeartCount;
        count = Math.Min(count, MechanicalHeartItem.HeartMax);
        return count > Index;
    }
    public override bool IsHidden()
    {
        int count = Main.LocalPlayer.GetModPlayer<MechanicalHeartPlayer>().MechanicalHeartCount;
        count = Math.Min(count, MechanicalHeartItem.HeartMax);
        return count < (Index + 1);
    }
}
public class MHAccS1 : MechanicalAccSlotBase
{
    protected override int Index => 0;
}
public class MHAccS2 : MechanicalAccSlotBase
{
    protected override int Index => 1;
}
public class MHAccS3 : MechanicalAccSlotBase
{
    protected override int Index => 2;
}
public class MHAccS4 : MechanicalAccSlotBase
{
    protected override int Index => 3;
}
public class MHAccS5 : MechanicalAccSlotBase
{
    protected override int Index => 4;
}
public class MHAccS6 : MechanicalAccSlotBase
{
    protected override int Index => 5;
}
public class MHAccS7 : MechanicalAccSlotBase
{
    protected override int Index => 6;
}
public class MHAccS8 : MechanicalAccSlotBase
{
    protected override int Index => 7;
}
public class MHAccS9 : MechanicalAccSlotBase
{
    protected override int Index => 8;
}
public class MHAccS10 : MechanicalAccSlotBase
{
    protected override int Index => 9;
}

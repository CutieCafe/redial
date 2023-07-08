using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox;
using Sandbox.Tools;
using Sandbox.UI;
using TerrorTown;

namespace Redial
{
    [Library("tool_redial_placer", Title = "Redial", Description = "Place TTT weapons - Press Reload for settings", Group = "fun")]
    public partial class RedialPlacer : BaseTool
    {
        internal static RedialPlacer Current;
        internal int CurrentWeaponIndex { get; set; } = 0;
        [Net] internal string CurrentWeapon { get; set; } = "";
        internal TypeDescription Item
        {
            get => GetQualifyingWeapons().Where(i => i.Name == CurrentWeapon).First();
        }
        [Net] internal string SavedTo { get; set; } = "";
        private TimeSince Saved { get; set; }
        private RedialPanel panel;

        private static readonly List<string> blacklisted = new() {
            "Pistol",
            "Weapon",
            "Gun",
            "Melee",
            "BaseAmmoItem",
            "Grenade",
            "BaseRandom",
            "IGrenade",
            "DebugGun"
        };

        private static RootPanel HUD
        {
            get => ((SandboxHud)Entity.All.Where(i => i.GetType() == typeof(SandboxHud)).First()).RootPanel;
        }

        public override void Activate()
        {
            Current = this;

            if (Game.IsClient)
            {
                panel = HUD.AddChild<RedialPanel>();
            }
        }

        public override void Deactivate()
        {
            base.Deactivate();

            if(Game.IsClient)
            {
                if (panel != null)
                {
                    panel.Delete();
                }
            }
        }

        public static IEnumerable<TypeDescription> GetQualifyingWeapons()
        {
            return TypeLibrary.GetTypes<TerrorTown.Weapon>().Where(i => !blacklisted.Contains(i.Name))
                    .Concat(TypeLibrary.GetTypes<BaseAmmoItem>().Where(i => !blacklisted.Contains(i.Name)))
                    .Concat(TypeLibrary.GetTypes<IGrenade>().Where(i => !blacklisted.Contains(i.Name)))
                    .Concat(TypeLibrary.GetTypes<BaseRandom>().Where(i => !blacklisted.Contains(i.Name)));

        }

        private static string SerializeEntity(Entity ent)
        {
            var name = ent.GetType().Name;

            if (ent.Tags.Has("random_grenade")) name = "RandomGrenade";
            else if (ent.Tags.Has("random_weapon")) name = "RandomWeapon";
            else if (ent.Tags.Has("random_ammo")) name = "RandomAmmo";

            return name + ";" + ent.Position.ToString() + ";" + ent.Rotation.ToString();
        }

        public override void Simulate()
        {
            if (Game.IsClient && panel == null)
            {
                Current = this;
                panel = HUD.AddChild<RedialPanel>();
            }

            if (Game.IsServer)
            {
                if(Input.Pressed("attack1") && CurrentWeapon != "")
                {
                    var tr = Trace.Ray(Owner.EyePosition, Owner.EyePosition + Owner.EyeRotation.Forward * 2000).UseHitboxes().WithAnyTags("solid", "debris").Ignore(Owner).Radius(2.0f).Run();

                    if(! tr.Hit || ! tr.Entity.IsValid())
                    {
                        return;
                    }

                    ModelEntity item;

                    if (Item.TargetType.IsAssignableTo(typeof(RandomGrenade)))
                    {
                        item = new IncendiaryGrenade();
                        item.RenderColor = Color.Magenta;
                        item.Tags.Add("random_grenade");
                    }
                    else if( Item.TargetType.IsAssignableTo(typeof(RandomWeapon)))
                    {
                        item = new Huge();
                        item.RenderColor = Color.Magenta;
                        item.Tags.Add("random_weapon");
                    }
                    else if(Item.TargetType.IsAssignableTo(typeof(RandomAmmo)))
                    {
                        item = new ShotgunAmmo();
                        item.RenderColor = Color.Magenta;
                        item.Tags.Add("random_ammo");
                    }
                    else {
                        item = Item.Create<ModelEntity>();
                    }

                    item.EnableDrawing = true;
                    item.EnableAllCollisions = true;
                    item.Position = tr.EndPosition;

                    item.Rotation = Rotation.LookAt(tr.Normal, Owner.EyeRotation.Forward) * Rotation.From(new Angles(90, 0, 0));

                    item.Tags.Add("redial");
                    item.Tags.Add("solid");

                    item.Spawn();
                }
            }
        }

        [ConCmd.Server("cutie_redial_weapon")]
        public static void SetWeapon(string weapon)
        {
            var myTools = Entity.All.Where(i => i is Tool tool && tool.Owner == ConsoleSystem.Caller?.Pawn && tool.CurrentTool is RedialPlacer);

            if( myTools.Any()) {
                ((myTools.First() as Tool).CurrentTool as RedialPlacer).CurrentWeapon = weapon;
            } else
            {
                Log.Error("No tool?!");
            }
        }

        [ConCmd.Server("cutie_redial_save")]
        public static void Save()
        {
            var ents = Entity.All.Where(i => i != null && i.Tags.Has("redial"));

            FileSystem.Data.CreateDirectory("redial");
            FileSystem.Data.WriteAllText("redial/" + Game.Server.MapIdent + ".redial", string.Join('\n', ents.Select(i => SerializeEntity(i))));

            Sandbox.Chat.AddChatEntry("Server", $"Saved Redial script to {FileSystem.Data.GetFullPath("redial/" + Game.Server.MapIdent + ".redial")}");
        }
    }
}

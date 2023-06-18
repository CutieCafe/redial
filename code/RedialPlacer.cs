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
    [Library("tool_redial_placer", Title = "Redial", Description = "Primary attack to place, secondary attack to switch weapon, reload to save", Group = "fun")]
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

            if (Game.IsServer)
            {
                CurrentWeapon = GetQualifyingWeapons().ElementAt(CurrentWeaponIndex).Name;
            }

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

        private static IEnumerable<TypeDescription> GetQualifyingWeapons()
        {
            return TypeLibrary.GetTypes<TerrorTown.Weapon>().Where(i => !blacklisted.Contains(i.Name))
                    .Concat(TypeLibrary.GetTypes<BaseAmmoItem>().Where(i => !blacklisted.Contains(i.Name)))
                    .Concat(TypeLibrary.GetTypes<IGrenade>().Where(i => !blacklisted.Contains(i.Name)))
                    .Concat(TypeLibrary.GetTypes<BaseRandom>().Where(i => !blacklisted.Contains(i.Name)));

        }

        private string SerializeEntity(Entity ent)
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
                if(CurrentWeapon == "")
                {
                    CurrentWeapon = GetQualifyingWeapons().ElementAt(CurrentWeaponIndex).Name;
                }

                if(Input.Pressed("attack1"))
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

                if (Input.Pressed("attack2"))
                {
                    CurrentWeaponIndex++;
                    if (CurrentWeaponIndex >= GetQualifyingWeapons().Count()) CurrentWeaponIndex = 0;
                    CurrentWeapon = GetQualifyingWeapons().ElementAt(CurrentWeaponIndex).Name;
                }

                if( Input.Pressed("reload"))
                {
                    SavedTo = FileSystem.Data.GetFullPath("redial/" + Game.Server.MapIdent + ".redial");

                    var ents = Entity.All.Where(i => i != null && i.Tags.Has("redial"));

                    FileSystem.Data.CreateDirectory("redial");
                    FileSystem.Data.WriteAllText("redial/" + Game.Server.MapIdent + ".redial", string.Join('\n', ents.Select(SerializeEntity)));
                    Saved = 0;
                }
            }

            if( SavedTo != "" )
            {
                DebugOverlay.ScreenText("Saved weapon remap to " + FileSystem.Data.GetFullPath("redial/" + Game.Server.MapIdent + ".redial"), new Vector2(20, 20));

                if(Saved > 10) {
                    SavedTo = "";
                }
            }
        }
    }
}

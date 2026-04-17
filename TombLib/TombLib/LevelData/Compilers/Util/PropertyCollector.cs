using System.Collections.Generic;
using TombLib.LuaProperties;
using TombLib.Wad.Catalog;

namespace TombLib.LevelData.Compilers.Util;

internal class PropertyCollector
{
    internal static PropertyCollection Generate(Level level)
    {
        var gameVersion = level.Settings.GameVersion;

        var globalMovProps = new Dictionary<string, LuaPropertyContainer>();
        var globalStaticProps = new Dictionary<uint, LuaPropertyContainer>();

        // Level 1: Global properties.

        foreach (var wadRef in level.Settings.Wads)
        {
            if (wadRef.Wad == null)
                continue;

            foreach (var mov in wadRef.Wad.Moveables)
            {
                string slotName = TrCatalog.GetMoveableName(gameVersion, mov.Key.TypeId);
                if (string.IsNullOrEmpty(slotName))
                    continue;

                // 1) Prefer properties from wad2.
                if (mov.Value.LuaProperties != null && mov.Value.LuaProperties.HasProperties)
                {
                    globalMovProps[slotName] = mov.Value.LuaProperties;
                    continue;
                }

                // 2) Fallback to catalog defaults (only if not already defined).
                if (globalMovProps.ContainsKey(slotName))
                    continue;

                var definitions = LuaPropertyCatalog.GetDefinitions(LuaPropertyObjectKind.Moveable, mov.Key.TypeId);
                if (definitions.Count == 0)
                    continue;

                var container = new LuaPropertyContainer();
                foreach (var def in definitions)
                    container.SetValue(def.InternalName, def.DefaultValue);

                globalMovProps[slotName] = container;
            }

            foreach (var stat in wadRef.Wad.Statics)
            {
                uint typeId = stat.Key.TypeId;

                // 1) Prefer properties from wad2.
                if (stat.Value.LuaProperties != null && stat.Value.LuaProperties.HasProperties)
                {
                    globalStaticProps[typeId] = stat.Value.LuaProperties;
                    continue;
                }

                // 2) Fallback to catalog defaults (only if not already defined).
                if (globalStaticProps.ContainsKey(typeId))
                    continue;

                var definitions = LuaPropertyCatalog.GetDefinitions(LuaPropertyObjectKind.Static, typeId);

                if (definitions.Count == 0)
                    continue;

                var container = new LuaPropertyContainer();
                foreach (var def in definitions)
                    container.SetValue(def.InternalName, def.DefaultValue);

                globalStaticProps[typeId] = container;
            }
        }

        // Level 2: Instance properties.

        var instanceMovProps = new Dictionary<string, LuaPropertyContainer>();
        var instanceStaticProps = new Dictionary<string, LuaPropertyContainer>();

        foreach (var room in level.ExistingRooms)
        {
            foreach (var obj in room.Objects)
            {
                if (obj is MoveableInstance mov && level.Settings.WadTryGetMoveable(mov.WadObjectId) != null &&
                    mov.LuaProperties?.HasProperties == true && !string.IsNullOrEmpty(mov.LuaName))
                {
                    instanceMovProps[mov.LuaName] = mov.LuaProperties;
                }
                else if (obj is StaticInstance stat && level.Settings.WadTryGetStatic(stat.WadObjectId) != null &&
                    stat.LuaProperties?.HasProperties == true && !string.IsNullOrEmpty(stat.LuaName))
                {
                    instanceStaticProps[stat.LuaName] = stat.LuaProperties;
                }
            }
        }

        bool hasAnyProperties = globalMovProps.Count > 0 || globalStaticProps.Count > 0 || instanceMovProps.Count > 0 || instanceStaticProps.Count > 0;
        return !hasAnyProperties ? null : new()
        {
            GlobalMoveables = globalMovProps,
            GlobalStatics = globalStaticProps,
            Moveables = instanceMovProps,
            Statics = instanceStaticProps,
        };
    }
}

internal class PropertyCollection
{
    public Dictionary<string, LuaPropertyContainer> GlobalMoveables { get; set; }
    public Dictionary<uint, LuaPropertyContainer> GlobalStatics { get; set; }
    public Dictionary<string, LuaPropertyContainer> Moveables { get; set; }
    public Dictionary<string, LuaPropertyContainer> Statics { get; set; }
}

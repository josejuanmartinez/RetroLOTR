using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class PcEffectCatalog
{
    public sealed class PcEffectDefinition
    {
        public string title;
        public string description;
        public Func<Character, bool> canExecute;
        public Func<Character, bool> execute;

        public bool CanExecute(Character character) => canExecute != null && canExecute(character);
        public bool Execute(Character character) => execute != null && execute(character);
        public bool PreferEffectForAi(Character character) => CanExecute(character);
    }

    public static PcEffectDefinition GetDefinition(string actionTypeName)
    {
        if (string.IsNullOrWhiteSpace(actionTypeName)) return null;

        string key = actionTypeName.Trim();
        return ResolveExplicit(key) ?? ResolveHeuristic(key);
    }

    private static PcEffectDefinition ResolveExplicit(string key)
    {
        if (key.Contains("Bree", StringComparison.OrdinalIgnoreCase)) return BreefolkHospitality();
        if (key.Contains("Imladris", StringComparison.OrdinalIgnoreCase)) return GreatElvenSanctuary();
        if (key.Contains("GreyHavens", StringComparison.OrdinalIgnoreCase)) return GreyHavensPassage();
        if (key.Contains("HennethAnnun", StringComparison.OrdinalIgnoreCase)) return RangerAmbush();
        if (key.Contains("Hobbiton", StringComparison.OrdinalIgnoreCase)) return HobbitHearth();
        if (key.Contains("Edoras", StringComparison.OrdinalIgnoreCase)) return RohirrimMuster();
        if (key.Contains("MinasTirith", StringComparison.OrdinalIgnoreCase)) return GondorianBastion();
        if (key.Contains("Orthanc", StringComparison.OrdinalIgnoreCase)) return DarkFortress();
        if (key.Contains("BaradDur", StringComparison.OrdinalIgnoreCase)) return DarkFortress();
        if (key.Contains("MinasMorgul", StringComparison.OrdinalIgnoreCase)) return UngolVenom();
        if (key.Contains("KhazadDum", StringComparison.OrdinalIgnoreCase)) return DwarvenForgeHall();
        if (key.Contains("Rhosgobel", StringComparison.OrdinalIgnoreCase)) return ForestSanctuary();
        if (key.Contains("HavensOfUmbar", StringComparison.OrdinalIgnoreCase)) return HaradPort();
        if (key.Contains("Thanduil", StringComparison.OrdinalIgnoreCase)) return ElvenRefuge();
        return null;
    }

    private static PcEffectDefinition ResolveHeuristic(string key)
    {
        if (MatchesAny(key, "Caras", "Cerin", "Elostirion", "Forlond", "Harlond", "Edhellond"))
        {
            return ElvenRefuge();
        }

        if (MatchesAny(key, "Minas", "Cair", "Pelargir", "DolAmroth", "Osgiliath", "Erech", "Linhir", "Morthondost"))
        {
            return GondorianBastion();
        }

        if (MatchesAny(key, "Edoras", "Eastfold", "WestFold", "Helm", "Dunharrow", "Aglarond", "Derndingle"))
        {
            return RohirrimMuster();
        }

        if (MatchesAny(key, "Khazad", "Belegost", "Noegrod", "Thorin", "Azanulimbar", "Barak", "Annuminas", "Fornost"))
        {
            return DwarvenForgeHall();
        }

        if (MatchesAny(key, "Buckland", "Michel", "Crickhollow", "OldForest", "Marish"))
        {
            return HobbitHearth();
        }

        if (MatchesAny(key, "Beorn", "Carrock", "Framsburg", "Gladden"))
        {
            return BeorningHall();
        }

        if (MatchesAny(key, "Rhosgobel", "Woodmen", "Druadan", "ErynVorn", "CeberFanuin", "Forest"))
        {
            return ForestSanctuary();
        }

        if (MatchesAny(key, "Eagle", "Caradhras", "Forochel"))
        {
            return EagleWatch();
        }

        if (MatchesAny(key, "Dale", "Esgaroth", "Maethelburg", "Riavod"))
        {
            return TradeWaterways();
        }

        if (MatchesAny(key, "Angren", "Arailt", "Enedhir", "Larach", "Treforn"))
        {
            return DunlandRaidCamp();
        }

        if (MatchesAny(key, "Goblin", "Gundabad", "CarnDum", "MtGram"))
        {
            return GoblinMuster();
        }

        if (MatchesAny(key, "Barad", "Orthanc", "DolGuldur", "Durthang", "MountDoom"))
        {
            return DarkFortress();
        }

        if (MatchesAny(key, "Ungol", "Shelob", "Cirith"))
        {
            return UngolVenom();
        }

        if (MatchesAny(key, "Morannon", "LugGhurzun"))
        {
            return MordorGate();
        }

        if (MatchesAny(key, "Ashkiri", "DeadMarshes", "LagVrasfotak", "MountainsOfMirkwood", "Ostigurth", "Thuringwathost"))
        {
            return AshenWastes();
        }

        if (MatchesAny(key, "KalNargil", "Luglurak", "Nurumurl", "Orduga", "Rul", "Urlurtsu"))
        {
            return NurnWarCamp();
        }

        if (MatchesAny(key, "Umbar", "Harad", "CarasMirilond", "CarasTolfalas", "JugRijeisha", "KasShadoul", "Lugarlur", "Methir", "Pellardur", "TolBuruth", "Wathduin", "Isigir"))
        {
            return HaradPort();
        }

        if (MatchesAny(key, "Khand", "KasShafra", "Lagari", "Neburcha", "Laorki", "Ovatharac", "AnKaragmir", "ButhOvaisa", "Sturlurtsa"))
        {
            return KhandRiderCamp();
        }

        if (MatchesAny(key, "Rhun", "Rhubar", "Elgaer", "Ilanin", "Carvarad", "ShrelKain", "RaiderHold"))
        {
            return RhunicCourt();
        }

        if (MatchesAny(key, "Barrow", "Paths"))
        {
            return ForgottenBarrows();
        }

        return RangerAmbush();
    }

    private static bool MatchesAny(string key, params string[] needles)
    {
        for (int i = 0; i < needles.Length; i++)
        {
            if (key.Contains(needles[i], StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    private static PcEffectDefinition Create(string title, string description, Func<Character, bool> canExecute, Func<Character, bool> execute)
    {
        return new PcEffectDefinition
        {
            title = title,
            description = description,
            canExecute = canExecute,
            execute = execute
        };
    }

    private static PcEffectDefinition BreefolkHospitality() => Create(
        "Breefolk Hospitality",
        "Nearby allied Humans and Hobbits gain Hope (1), and your nation gains +1 <sprite name=\"gold\"/>.",
        c => HasAlliedCharacters(c, 1, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Hobbit) || HasOwner(c),
        c =>
        {
            int blessed = ApplyToAlliedCharacters(c, 1, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Hobbit, ch => ch.ApplyStatusEffect(StatusEffectEnum.Hope, 1));
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Breefolk Hospitality grants Hope to {blessed} nearby ally(s) and +1 gold.", Color.green);
            return blessed > 0 || HasOwner(c);
        });

    private static PcEffectDefinition ElvenRefuge() => Create(
        "Elven Refuge",
        "Allied Elves and allied non-army characters in radius 2 gain Hidden (1) and Encouraged (1).",
        c => HasAlliedCharacters(c, 2, ch => ch.race == RacesEnum.Elf || !ch.IsArmyCommander()),
        c => ApplyStatuses(c, AlliedCharacters(c, 2, ch => ch.race == RacesEnum.Elf || !ch.IsArmyCommander()), "Elven Refuge", (StatusEffectEnum.Hidden, 1), (StatusEffectEnum.Encouraged, 1), Color.cyan));

    private static PcEffectDefinition GreatElvenSanctuary() => Create(
        "Great Elven Sanctuary",
        "Allied characters in radius 2 gain Hope (1); allied Elves and mage-capable allies there also gain ArcaneInsight (1).",
        c => HasAlliedCharacters(c, 2),
        c =>
        {
            List<Character> targets = AlliedCharacters(c, 2);
            int insight = 0;
            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                if (target.race == RacesEnum.Elf || target.GetMage() > 0)
                {
                    target.ApplyStatusEffect(StatusEffectEnum.ArcaneInsight, 1);
                    insight++;
                }
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Great Elven Sanctuary grants Hope to {targets.Count} ally(s) and ArcaneInsight to {insight}.", Color.cyan);
            return targets.Count > 0;
        });

    private static PcEffectDefinition GreyHavensPassage() => Create(
        "Grey Havens Passage",
        "Allied characters on shore or water in radius 2 gain Haste (1) and Hope (1), and your nation gains +1 <sprite name=\"gold\"/>.",
        c => ShoreOrWaterAllies(c, 2).Count > 0 || HasOwner(c),
        c =>
        {
            List<Character> targets = ShoreOrWaterAllies(c, 2);
            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
                target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Grey Havens Passage speeds {targets.Count} shorebound ally(s) and adds +1 gold.", Color.cyan);
            return targets.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition DwarvenForgeHall() => Create(
        "Dwarven Forge-Hall",
        "Allied Dwarves in radius 2 gain Fortified (1) and Strengthened (1), and your nation gains +1 <sprite name=\"gold\"/>.",
        c => HasAlliedCharacters(c, 2, ch => ch.race == RacesEnum.Dwarf) || HasOwner(c),
        c =>
        {
            List<Character> dwarves = AlliedCharacters(c, 2, ch => ch.race == RacesEnum.Dwarf);
            foreach (Character dwarf in dwarves)
            {
                dwarf.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                dwarf.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            }
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Dwarven Forge-Hall steels {dwarves.Count} dwarf ally(s) and adds +1 gold.", Color.yellow);
            return dwarves.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition RohirrimMuster() => Create(
        "Rohirrim Muster",
        "Allied army commanders in radius 2 gain Haste (1) and Encouraged (1).",
        c => HasAlliedCharacters(c, 2, ch => ch.IsArmyCommander()),
        c => ApplyStatuses(c, AlliedCharacters(c, 2, ch => ch.IsArmyCommander()), "Rohirrim Muster", (StatusEffectEnum.Haste, 1), (StatusEffectEnum.Encouraged, 1), Color.yellow));

    private static PcEffectDefinition GondorianBastion() => Create(
        "Gondorian Bastion",
        "Allied army commanders in radius 2 gain Fortified (1); allied Humans and Dunedain there gain Hope (1).",
        c => HasAlliedCharacters(c, 2, ch => ch.IsArmyCommander() || ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain),
        c =>
        {
            List<Character> targets = AlliedCharacters(c, 2, ch => ch.IsArmyCommander() || ch.race == RacesEnum.Common || ch.race == RacesEnum.Dunedain);
            foreach (Character target in targets)
            {
                if (target.IsArmyCommander()) target.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
                if (target.race == RacesEnum.Common || target.race == RacesEnum.Dunedain) target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Gondorian Bastion fortifies and renews {targets.Count} allied unit(s).", Color.white);
            return targets.Count > 0;
        });

    private static PcEffectDefinition RangerAmbush() => Create(
        "Ranger Ambush",
        "Allied non-army characters in radius 2 gain Hidden (1); enemy characters in radius 1 gain Halted (1).",
        c => HasAlliedCharacters(c, 2, ch => !ch.IsArmyCommander()) || HasEnemyCharacters(c, 1),
        c =>
        {
            List<Character> allies = AlliedCharacters(c, 2, ch => !ch.IsArmyCommander());
            List<Character> enemies = EnemyCharacters(c, 1);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Ranger Ambush veils {allies.Count} ally(s) and halts {enemies.Count} enemy unit(s).", Color.green);
            return allies.Count > 0 || enemies.Count > 0;
        });

    private static PcEffectDefinition HobbitHearth() => Create(
        "Hobbit Hearth",
        "Allied Hobbits and nearby friendly small folk in radius 2 gain Hope (1); allied Hobbits there also gain Hidden (1), and your nation gains +1 <sprite name=\"gold\"/>.",
        c => HasAlliedCharacters(c, 2, ch => ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Common) || HasOwner(c),
        c =>
        {
            List<Character> targets = AlliedCharacters(c, 2, ch => ch.race == RacesEnum.Hobbit || ch.race == RacesEnum.Common);
            int hidden = 0;
            foreach (Character target in targets)
            {
                target.ApplyStatusEffect(StatusEffectEnum.Hope, 1);
                if (target.race == RacesEnum.Hobbit)
                {
                    target.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                    hidden++;
                }
            }
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Hobbit Hearth cheers {targets.Count} ally(s), hides {hidden} hobbit(s), and adds +1 gold.", Color.green);
            return targets.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition BeorningHall() => Create(
        "Beorning Hall",
        "Allied Humans and Beornings in radius 2 gain Strengthened (1); allied commanders there gain Haste (1).",
        c => HasAlliedCharacters(c, 2, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Beorning || ch.IsArmyCommander()),
        c =>
        {
            List<Character> targets = AlliedCharacters(c, 2, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Beorning || ch.IsArmyCommander());
            foreach (Character target in targets)
            {
                if (target.race == RacesEnum.Common || target.race == RacesEnum.Beorning) target.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                if (target.IsArmyCommander()) target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Beorning Hall emboldens {targets.Count} allied unit(s).", Color.yellow);
            return targets.Count > 0;
        });

    private static PcEffectDefinition ForestSanctuary() => Create(
        "Forest Sanctuary",
        "Allied non-army characters in radius 2 gain Hidden (1); allied Woodfolk, Beornings, Woses, and Ents there gain Encouraged (1).",
        c => HasAlliedCharacters(c, 2, ch => !ch.IsArmyCommander() || ch.race == RacesEnum.Beorning || ch.race == RacesEnum.Wose || ch.race == RacesEnum.Ent),
        c =>
        {
            List<Character> targets = AlliedCharacters(c, 2, ch => !ch.IsArmyCommander() || ch.race == RacesEnum.Beorning || ch.race == RacesEnum.Wose || ch.race == RacesEnum.Ent);
            foreach (Character target in targets)
            {
                if (!target.IsArmyCommander()) target.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
                if (target.race == RacesEnum.Beorning || target.race == RacesEnum.Wose || target.race == RacesEnum.Ent || target.race == RacesEnum.Common) target.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Forest Sanctuary shields {targets.Count} allied unit(s).", Color.green);
            return targets.Count > 0;
        });

    private static PcEffectDefinition EagleWatch() => Create(
        "Eagle Watch",
        "Allied non-army characters in radius 2 gain Haste (1) and Hidden (1).",
        c => HasAlliedCharacters(c, 2, ch => !ch.IsArmyCommander()),
        c => ApplyStatuses(c, AlliedCharacters(c, 2, ch => !ch.IsArmyCommander()), "Eagle Watch", (StatusEffectEnum.Haste, 1), (StatusEffectEnum.Hidden, 1), Color.cyan));

    private static PcEffectDefinition TradeWaterways() => Create(
        "Trade Waterways",
        "Your nation gains +1 <sprite name=\"gold\"/>; allied shore and river travellers in radius 2 gain Haste (1).",
        c => HasOwner(c) || ShoreOrWaterAllies(c, 2).Count > 0,
        c =>
        {
            List<Character> targets = ShoreOrWaterAllies(c, 2);
            foreach (Character target in targets) target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Trade Waterways speed {targets.Count} allied traveller(s) and add +1 gold.", Color.yellow);
            return targets.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition DunlandRaidCamp() => Create(
        "Dunland Raid-Camp",
        "Allied Human, Orc, and Goblin units in radius 2 gain Strengthened (1); allied commanders there gain Haste (1).",
        c => HasAlliedCharacters(c, 2, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin || ch.IsArmyCommander()),
        c =>
        {
            List<Character> targets = AlliedCharacters(c, 2, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Orc || ch.race == RacesEnum.Goblin || ch.IsArmyCommander());
            foreach (Character target in targets)
            {
                if (target.race == RacesEnum.Common || target.race == RacesEnum.Orc || target.race == RacesEnum.Goblin) target.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
                if (target.IsArmyCommander()) target.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            }
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Dunland Raid-Camp hardens {targets.Count} allied unit(s).", Color.white);
            return targets.Count > 0;
        });

    private static PcEffectDefinition GoblinMuster() => Create(
        "Goblin Muster",
        "Allied Orcs, Goblins, and Trolls in radius 2 gain Strengthened (1); enemy characters in radius 1 gain Fear (1).",
        c => HasAlliedCharacters(c, 2, IsDarkRace) || HasEnemyCharacters(c, 1),
        c =>
        {
            List<Character> allies = AlliedCharacters(c, 2, IsDarkRace);
            List<Character> enemies = EnemyCharacters(c, 1);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Goblin Muster strengthens {allies.Count} ally(s) and spreads Fear to {enemies.Count} enemy unit(s).", Color.red);
            return allies.Count > 0 || enemies.Count > 0;
        });

    private static PcEffectDefinition DarkFortress() => Create(
        "Dark Fortress",
        "Enemy characters in radius 2 gain Fear (1); allied dark units and commanders there gain Fortified (1).",
        c => HasEnemyCharacters(c, 2) || HasAlliedCharacters(c, 2, ch => IsDarkRace(ch) || ch.IsArmyCommander()),
        c =>
        {
            List<Character> enemies = EnemyCharacters(c, 2);
            List<Character> allies = AlliedCharacters(c, 2, ch => IsDarkRace(ch) || ch.IsArmyCommander());
            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Fortified, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Dark Fortress spreads Fear to {enemies.Count} enemy unit(s) and fortifies {allies.Count} ally(s).", Color.red);
            return enemies.Count > 0 || allies.Count > 0;
        });

    private static PcEffectDefinition MordorGate() => Create(
        "Mordor Gate",
        "Enemy characters in radius 1 gain Halted (1); allied commanders in radius 2 gain Haste (1).",
        c => HasEnemyCharacters(c, 1) || HasAlliedCharacters(c, 2, ch => ch.IsArmyCommander()),
        c =>
        {
            List<Character> enemies = EnemyCharacters(c, 1);
            List<Character> allies = AlliedCharacters(c, 2, ch => ch.IsArmyCommander());
            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Halted, 1);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Mordor Gate halts {enemies.Count} enemy unit(s) and hastes {allies.Count} allied commander(s).", Color.red);
            return enemies.Count > 0 || allies.Count > 0;
        });

    private static PcEffectDefinition UngolVenom() => Create(
        "Ungol Venom",
        "Enemy characters in your hex gain Poisoned and Fear; allied dark units in radius 1 gain Hidden (1).",
        c => EnemyCharacters(c, 0).Count > 0 || HasAlliedCharacters(c, 1, IsDarkRace),
        c =>
        {
            List<Character> enemies = EnemyCharacters(c, 0);
            List<Character> allies = AlliedCharacters(c, 1, IsDarkRace);
            foreach (Character enemy in enemies)
            {
                enemy.ApplyStatusEffect(StatusEffectEnum.Poisoned, 1);
                enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            }
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Ungol Venom poisons {enemies.Count} foe(s) and veils {allies.Count} dark ally(s).", Color.magenta);
            return enemies.Count > 0 || allies.Count > 0;
        });

    private static PcEffectDefinition AshenWastes() => Create(
        "Ashen Wastes",
        "Enemy characters in radius 2 gain Fear (1); allied dark units there gain Strengthened (1).",
        c => HasEnemyCharacters(c, 2) || HasAlliedCharacters(c, 2, IsDarkRace),
        c =>
        {
            List<Character> enemies = EnemyCharacters(c, 2);
            List<Character> allies = AlliedCharacters(c, 2, IsDarkRace);
            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Ashen Wastes unnerve {enemies.Count} foe(s) and embolden {allies.Count} dark ally(s).", Color.red);
            return enemies.Count > 0 || allies.Count > 0;
        });

    private static PcEffectDefinition NurnWarCamp() => Create(
        "Nurn War-Camp",
        "Allied dark units and commanders in radius 2 gain Strengthened (1); your nation gains +1 <sprite name=\"gold\"/>.",
        c => HasAlliedCharacters(c, 2, ch => IsDarkRace(ch) || ch.IsArmyCommander()) || HasOwner(c),
        c =>
        {
            List<Character> allies = AlliedCharacters(c, 2, ch => IsDarkRace(ch) || ch.IsArmyCommander());
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Strengthened, 1);
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Nurn War-Camp strengthens {allies.Count} ally(s) and adds +1 gold.", Color.red);
            return allies.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition HaradPort() => Create(
        "Harad Port",
        "Your nation gains +2 <sprite name=\"gold\"/>; allied commanders in radius 2 gain Haste (1).",
        c => HasOwner(c) || HasAlliedCharacters(c, 2, ch => ch.IsArmyCommander()),
        c =>
        {
            List<Character> allies = AlliedCharacters(c, 2, ch => ch.IsArmyCommander());
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Haste, 1);
            AddGold(c, 2);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Harad Port hastes {allies.Count} allied commander(s) and adds +2 gold.", Color.yellow);
            return allies.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition KhandRiderCamp() => Create(
        "Khand Rider-Camp",
        "Allied commanders in radius 2 gain Haste (1) and Encouraged (1).",
        c => HasAlliedCharacters(c, 2, ch => ch.IsArmyCommander()),
        c => ApplyStatuses(c, AlliedCharacters(c, 2, ch => ch.IsArmyCommander()), "Khand Rider-Camp", (StatusEffectEnum.Haste, 1), (StatusEffectEnum.Encouraged, 1), Color.yellow));

    private static PcEffectDefinition RhunicCourt() => Create(
        "Rhunic Court",
        "Your nation gains +1 <sprite name=\"gold\"/>; allied Humans, Southrons, and Easterlings in radius 2 gain Encouraged (1).",
        c => HasOwner(c) || HasAlliedCharacters(c, 2, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Southron || ch.race == RacesEnum.Easterling),
        c =>
        {
            List<Character> allies = AlliedCharacters(c, 2, ch => ch.race == RacesEnum.Common || ch.race == RacesEnum.Southron || ch.race == RacesEnum.Easterling);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Encouraged, 1);
            AddGold(c, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Rhunic Court heartens {allies.Count} ally(s) and adds +1 gold.", Color.yellow);
            return allies.Count > 0 || HasOwner(c);
        });

    private static PcEffectDefinition ForgottenBarrows() => Create(
        "Forgotten Barrows",
        "Enemy non-army characters in radius 1 gain Fear (1); allied non-army characters there gain Hidden (1).",
        c => HasEnemyCharacters(c, 1, ch => !ch.IsArmyCommander()) || HasAlliedCharacters(c, 1, ch => !ch.IsArmyCommander()),
        c =>
        {
            List<Character> enemies = EnemyCharacters(c, 1, ch => !ch.IsArmyCommander());
            List<Character> allies = AlliedCharacters(c, 1, ch => !ch.IsArmyCommander());
            foreach (Character enemy in enemies) enemy.ApplyStatusEffect(StatusEffectEnum.Fear, 1);
            foreach (Character ally in allies) ally.ApplyStatusEffect(StatusEffectEnum.Hidden, 1);
            MessageDisplayNoUI.ShowMessage(c.hex, c, $"Forgotten Barrows chills {enemies.Count} foe(s) and veils {allies.Count} ally(s).", Color.magenta);
            return enemies.Count > 0 || allies.Count > 0;
        });

    private static bool ApplyStatuses(Character source, List<Character> targets, string label, (StatusEffectEnum status, int turns) first, (StatusEffectEnum status, int turns) second, Color color)
    {
        foreach (Character target in targets)
        {
            target.ApplyStatusEffect(first.status, first.turns);
            target.ApplyStatusEffect(second.status, second.turns);
        }
        MessageDisplayNoUI.ShowMessage(source.hex, source, $"{label} affects {targets.Count} allied unit(s).", color);
        return targets.Count > 0;
    }

    private static bool HasOwner(Character character)
    {
        return character != null && character.GetOwner() != null;
    }

    private static void AddGold(Character character, int amount)
    {
        if (character == null || amount <= 0) return;
        character.GetOwner()?.AddGold(amount);
    }

    private static bool IsDarkRace(Character character)
    {
        if (character == null) return false;
        return character.race == RacesEnum.Orc
            || character.race == RacesEnum.Goblin
            || character.race == RacesEnum.Troll
            || character.race == RacesEnum.Nazgul
            || character.race == RacesEnum.Undead
            || character.race == RacesEnum.Spider;
    }

    private static bool IsAllied(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return true;
        return source.GetAlignment() != AlignmentEnum.neutral
            && target.GetAlignment() == source.GetAlignment()
            && target.GetAlignment() != AlignmentEnum.neutral;
    }

    private static bool IsEnemy(Character source, Character target)
    {
        if (source == null || target == null) return false;
        if (target.GetOwner() == source.GetOwner()) return false;
        if (target.GetAlignment() == AlignmentEnum.neutral) return true;
        return target.GetAlignment() != source.GetAlignment();
    }

    private static bool HasAlliedCharacters(Character source, int radius, Func<Character, bool> filter = null) => AlliedCharacters(source, radius, filter).Count > 0;
    private static bool HasEnemyCharacters(Character source, int radius, Func<Character, bool> filter = null) => EnemyCharacters(source, radius, filter).Count > 0;

    private static int ApplyToAlliedCharacters(Character source, int radius, Func<Character, bool> filter, Action<Character> apply)
    {
        List<Character> targets = AlliedCharacters(source, radius, filter);
        foreach (Character target in targets) apply(target);
        return targets.Count;
    }

    private static List<Character> AlliedCharacters(Character source, int radius, Func<Character, bool> filter = null)
    {
        return CollectCharacters(source, radius, ch => IsAllied(source, ch) && (filter == null || filter(ch)));
    }

    private static List<Character> EnemyCharacters(Character source, int radius, Func<Character, bool> filter = null)
    {
        return CollectCharacters(source, radius, ch => IsEnemy(source, ch) && (filter == null || filter(ch)));
    }

    private static List<Character> ShoreOrWaterAllies(Character source, int radius)
    {
        if (source == null || source.hex == null) return new List<Character>();
        return source.hex.GetHexesInRadius(radius)
            .Where(h => h != null && (h.terrainType == TerrainEnum.shore || h.IsWaterTerrain()) && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && IsAllied(source, ch))
            .Distinct()
            .ToList();
    }

    private static List<Character> CollectCharacters(Character source, int radius, Func<Character, bool> predicate)
    {
        if (source == null || source.hex == null) return new List<Character>();

        return source.hex.GetHexesInRadius(radius)
            .Where(h => h != null && h.characters != null)
            .SelectMany(h => h.characters)
            .Where(ch => ch != null && !ch.killed && predicate(ch))
            .Distinct()
            .ToList();
    }
}

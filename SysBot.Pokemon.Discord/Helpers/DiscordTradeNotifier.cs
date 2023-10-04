using Discord;
using Discord.WebSocket;
using PKHeX.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Discord.Commands;
using MathNet.Numerics.LinearAlgebra.Factorization;
using System.Text;
using System.Text.RegularExpressions;

namespace SysBot.Pokemon.Discord
{
    public class DiscordTradeNotifier<T> : IPokeTradeNotifier<T> where T : PKM, new()
    {
        private T Data { get; }
        private PokeTradeTrainerInfo Info { get; }
        private int Code { get; }
        private SocketUser Trader { get; }
        private SocketCommandContext Context { get; }
        public Action<PokeRoutineExecutor<T>>? OnFinish { private get; set; }
        public readonly PokeTradeHub<T> Hub = SysCord<T>.Runner.Hub;

        public DiscordTradeNotifier(T data, PokeTradeTrainerInfo info, int code, SocketUser trader, SocketCommandContext channel)
        {
            Data = data;
            Info = info;
            Code = code;
            Trader = trader;
            Context = channel;
        }

        public void TradeInitialize(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
        {
            var receive = Data.Species == 0 ? string.Empty : $" ({Data.Nickname})";
            Trader.SendMessageAsync($"Iniciando __trade__ **{receive}**. Porfavor este atento. Tu codigo es: **{Code:0000 0000}**.").ConfigureAwait(false);
        }

        public void TradeSearching(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info)
        {
            var name = Info.TrainerName;
            var trainer = string.IsNullOrEmpty(name) ? string.Empty : $" {name}";
            Trader.SendMessageAsync($"Estoy esperando por ti, **{trainer}**! __Tienes **40 segundos**__. Tu codigo es: **{Code:0000 0000}**. Mi IGN es **{routine.InGameName}**.").ConfigureAwait(false);
        }

        public void TradeCanceled(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeResult msg)
        {
            OnFinish?.Invoke(routine);
            Trader.SendMessageAsync($"✘ Trade __cancelado__: {msg}").ConfigureAwait(false);
        }

        public void TradeFinished(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result)
        {
            OnFinish?.Invoke(routine);
            var tradedToUser = Data.Species;
            var message = tradedToUser != 0 ? $"✓ Trade finalizado. Disfruta de tu **{(Species)tradedToUser}**!" : "✔ Trade finalizado!";
            Trader.SendMessageAsync(message).ConfigureAwait(false);
            if (result.Species != 0 && Hub.Config.Discord.ReturnPKMs)
                Trader.SendPKMAsync(result, "▼ Aqui esta lo que me enviaste! ▼").ConfigureAwait(false);

            var SVmon = TradeExtensions<PK9>.SVTrade;
            var LAmon = TradeExtensions<PA8>.LATrade;
            var BDSPmon = TradeExtensions<PB8>.BDSPTrade;
            var SWSHmon = TradeExtensions<PK8>.SWSHTrade;
            PKM fin = result;
            switch (result)
            {
                case PK9: fin = SVmon; break;
                case PA8: fin = LAmon; break;
                case PB8: fin = BDSPmon; break;
                case PK8: fin = SWSHmon; break;
            }

            if (fin.Species != 0 && Hub.Config.Trade.TradeDisplay && fin is PK9 pk9)
            {
                var shiny = fin.ShinyXor == 0 ? "<:square:1134580807529398392>" : fin.ShinyXor <= 16 ? "<:shiny:1134580552926777385>" : "";
                var set = new ShowdownSet($"{fin.Species}");
                var ballImg = $"https://raw.githubusercontent.com/BakaKaito/HomeImages/main/Ballimg/50x50/" + $"{(Ball)fin.Ball}ball".ToLower() + ".png";
                var gender = fin.Gender == 0 ? " - <:Males:1134568420843728917>" : fin.Gender == 1 ? " - <:Females:1134568421787435069>" : "";
                var pokeImg = TradeExtensions<T>.PokeImg(fin, false, false);
                string scale = "";
                if (fin is PK9 fin9)
                {
                    string scaleRating = PokeSizeDetailedUtil.GetSizeRating(fin9.Scale).ToString();

                    Dictionary<string, string> scaleEmojis = new Dictionary<string, string>
                    {
                       { "XXXS", "<:minimark:1158632782013136946>" }, // Replace "emoji_name_1" with the actual name of the emoji for XXXS
                       { "XXXL", "<:jumbomark:1158632783380492318>" }  // Replace "emoji_name_2" with the actual name of the emoji for XXXL
                    };

                    // Check if the scale value has a corresponding emoji
                    if (scaleEmojis.TryGetValue(scaleRating, out string? emojiCode))
                    {
                        // Use the emoji code in the message
                        scale = $"**Tamaño**: {emojiCode} {scaleRating} ({fin9.Scale})\n";
                    }
                    else
                    {
                        // If no emoji is found, just display the scale text
                        scale = $"**Tamaño**: {scaleRating} ({fin9.Scale})\n";
                    }
                }

                var tera = pk9.TeraType.ToString(); // Convertir a string 
                // Diccionario que mapea cada tipo de Tera a su correspondiente emoji en Discord
                Dictionary<string, string> teraEmojis = new Dictionary<string, string>
                {
                    { "Normal", "<:Normal:1134575677648162886>" },
                    { "Fire", "<:Fire:1134576993799766197>" },
                    { "Water", "<:Water:1134575004038742156>" },
                    { "Grass", "<:Grass:1134574800057139331>" },
                    { "Flying", "<:Flying:1134573296734711918>" },
                    { "Poison", "<:Poison:1134575188403564624>" },
                    { "Electric", "<:Electric:1134576561991995442>" },
                    { "Ground", "<:Ground:1134573701766058095>" },
                    { "Psychic", "<:Psychic:1134576746298089575>" },
                    { "Fighting", "<:Fighting:1134573062881300551>" },
                    { "Rock", "<:Rock:1134574024542912572>" },
                    { "Ice", "<:Ice:1134576183787409531>" },
                    { "Bug", "<:Bug:1134574602908073984>" },
                    { "Dragon", "<:Dragon:1134576015973294221>" },
                    { "Ghost", "<:Ghost:1134574276628975626>" },
                    { "Dark", "<:Dark:1134575488598294578>" },
                    { "Steel", "<:Steel:1134576384191254599>" },
                    { "Fairy", "<:Fairy:1134575841523814470>" },
                };
                var trademessage = $"**Nivel**: {fin.CurrentLevel}\n";
                // Obtener el emoji correspondiente al tipo de Tera
                if (teraEmojis.TryGetValue(tera, out string? emojiID))
                {
                    // Obtener el emoji desde el servidor de Discord utilizando el ID
                    var emoji = new Emoji(emojiID);

                    // Agregar el emoji al mensaje utilizando la sintaxis {emoji}
                    trademessage += $"**Tera**: {emoji} {tera}\n";
                }
                else
                {
                    // Si no se encuentra el emoji correspondiente, simplemente se muestra el tipo de Tera sin emoji
                    trademessage += $"**Tera**: {tera}\n";
                }
                trademessage += $"**Habilidad**: {GameInfo.GetStrings(1).Ability[fin.Ability]}\n";
                trademessage += $"**Naturaleza**: {(Nature)fin.Nature}\n{scale}";
                string ivMessage;

                // Check if all IVs are 31
                if (fin.IV_HP == 31 && fin.IV_ATK == 31 && fin.IV_DEF == 31 && fin.IV_SPA == 31 && fin.IV_SPD == 31 && fin.IV_SPE == 31)
                {
                    ivMessage = "**IVs**: Maximos";
                }
                else
                {
                    ivMessage = $"**IVs**: {fin.IV_HP}/{fin.IV_ATK}/{fin.IV_DEF}/{fin.IV_SPA}/{fin.IV_SPD}/{fin.IV_SPE}";
                }

                // Add the IVs information to the trade message
                trademessage += ivMessage + "\n";
                var evs = new List<string>();

                // Agregar los EVs no nulos al listado
                if (fin.EV_HP != 0)
                    evs.Add($"{fin.EV_HP} HP");

                if (fin.EV_ATK != 0)
                    evs.Add($"{fin.EV_ATK} Atk");

                if (fin.EV_DEF != 0)
                    evs.Add($"{fin.EV_DEF} Def");

                if (fin.EV_SPA != 0)
                    evs.Add($"{fin.EV_SPA} SpA");

                if (fin.EV_SPD != 0)
                    evs.Add($"{fin.EV_SPD} SpD");

                if (fin.EV_SPE != 0)
                    evs.Add($"{fin.EV_SPE} Spe");

                // Comprobar si hay EVs para agregarlos al mensaje
                if (evs.Any())
                {
                    trademessage += "**EVs**: " + string.Join(" / ", evs) + "\n";
                }
                var moves = new List<string>();

                //Remueve el None si no encuentra un movimiento
                if (fin.Move1 != 0)
                    moves.Add($"{(Move)fin.Move1}");
                if (fin.Move2 != 0)
                    moves.Add($"{(Move)fin.Move2}");
                if (fin.Move3 != 0)
                    moves.Add($"{(Move)fin.Move3}");
                if (fin.Move4 != 0)
                    moves.Add($"{(Move)fin.Move4}");

                // Add emojis for specific moves
                var waterEmoji = "<:move_water:1135381853675716728>"; // Water emoji
                var fireEmoji = "<:move_fire:1135381665028522025>"; // Fire emoji
                var electricEmoji = "<:move_electric:1135381611748270211>"; // Electric emoji
                var bugEmoji = "<:move_bug:1135381533750984794>"; // Bug Emoji
                var darkEmoji = "<:move_dark:1135381573588496414>"; // Dark Emoji
                var ghostEmoji = "<:move_ghost:1135381691465203733>"; //Ghost emoji
                var poisonEmoji = "<:move_poison:1135381791788765255>"; //Poison emoji
                var iceEmoji = "<:move_ice:1135381764223799356>"; //Ice emoji
                var steelEmoji = "<:move_steel:1135381836823011408>"; //steel emoji
                var rockEmoji = "<:move_rock:1135381815889252432>"; //rock emoji
                var groundEmoji = "<:move_ground:1135381748360954027>"; //ground emoji
                var fairyEmoji = "<:move_fairy:1135381627053297704>"; //fairy emoji
                var grassEmoji = "<:move_grass:1135381703796469780>"; //grass emoji
                var fightingEmoji = "<:move_fighting:1135381642878398464>"; //fighting emoji
                var normalEmoji = "<:move_normal:1135381779247804447>"; //normal emoji
                var dragonEmoji = "<:move_dragon:1135381595935752375>"; //dragon emoji
                var flyingEmoji = "<:move_flying:1135381678429315262>"; //flying emoji
                var psychicEmoji = "<:move_psychic:1135381805290229770>"; //pyschic emoji


                // move list
                var waterMoves = new List<string> { "WaterGun", "HydroPump", "Surf", "BubbleBeam", "Withdraw", "Waterfall", "Clamp", "Bubble", "Crabhammer", "Octazooka", "RainDance", "Whirlpool", "Dive", "HydroCannon", "WaterSpout", "MuddyWater", "WaterSport", "WaterPulse", "Brine", "AquaRing", "AquaTail", "AquaJet", "Soak", "Scald", "WaterPledge", "RazorShell", "SteamEruption", "WaterShuriken", "OriginPulse", "HydroVortex", "HydroVortex", "SparklingAria", "OceanicOperetta", "Liquidation", "SplishySplash", "BouncyBubble", "SnipeShot", "FishiousRend", "MaxGeyser", "LifeDew", "FlipTurn", "SurgingStrikes", "WaveCrash", "JetPunch", "TripleDive", "AquaStep", "HydroSteam", "ChillingWater", "AquaCutter" };
                var fireMoves = new List<string> { "WillOWisp", "FirePunch", "Ember", "Flamethrower", "FireSpin", "FireBlast", "FlameWheel", "SacredFire", "SunnyDay", "HeatWave", "Eruption", "BlazeKick", "BlastBurn", "Overheat", "FlareBlitz", "FireFang", "LavaPlume", "MagmaStorm", "FlameBurst", "FlameCharge", "Incinerate", "Inferno", "FirePledge", "HeatCrash", "SearingShot", "BlueFlare", "FieryDance", "V-create", "FusionFlare", "MysticalFire", "InfernoOverdrive", "InfernoOverdrive", "FireLash", "BurnUp", "ShellTrap", "MindBlown", "SizzlySlide", "MaxFlare", "PyroBall", "BurningJealousy", "RagingFury", "TorchSong", "ArmorCannon", "BitterBlade", "BlazingTorque" };
                var electricMoves = new List<string> { "ThunderPunch", "ThunderShock", "Thunderbolt", "ThunderWave", "Thunder", "ZapCannon", "Spark", "Charge", "VoltTackle", "ShockWave", "MagnetRise", "ThunderFang", "Discharge", "ChargeBeam", "ElectroBall", "VoltSwitch", "Electroweb", "WildCharge", "BoltStrike", "FusionBolt", "IonDeluge", "ParabolicCharge", "Electrify", "EerieImpulse", "MagneticFlux", "ElectricTerrain", "Nuzzle", "GigavoltHavoc", "GigavoltHavoc", "Catastropika", "StokedSparksurfer", "ZingZap", "10,000,000VoltThunderbolt", "PlasmaFists", "ZippyZap", "PikaPapow", "BuzzyBuzz", "BoltBeak", "MaxLightning", "AuraWheel", "Overdrive", "RisingVoltage", "ThunderCage", "WildboltStorm", "ElectroDrift", "DoubleShock" };
                var bugEmojiMoves = new List<string> { "XScissor", "Uturn", "Twineedle", "PinMissile", "StringShot", "LeechLife", "SpiderWeb", "FuryCutter", "Megahorn", "TailGlow", "SilverWind", "SignalBeam", "U-turn", "X-Scissor", "BugBuzz", "BugBite", "AttackOrder", "DefendOrder", "HealOrder", "RagePowder", "QuiverDance", "StruggleBug", "Steamroller", "StickyWeb", "FellStinger", "Powder", "Infestation", "SavageSpin-Out", "SavageSpin-Out", "FirstImpression", "PollenPuff", "Lunge", "MaxFlutterby", "SkitterSmack", "SilkTrap", "Pounce" };
                var darkMoves = new List<string> { "Bite", "Thief", "FeintAttack", "Pursuit", "Crunch", "BeatUp", "Torment", "Flatter", "Memento", "Taunt", "KnockOff", "Snatch", "FakeTears", "Payback", "Assurance", "Embargo", "Fling", "Punishment", "SuckerPunch", "DarkPulse", "NightSlash", "Switcheroo", "NastyPlot", "DarkVoid", "HoneClaws", "FoulPlay", "Quash", "NightDaze", "Snarl", "PartingShot", "Topsy-Turvy", "HyperspaceFury", "BlackHoleEclipse", "BlackHoleEclipse", "DarkestLariat", "ThroatChop", "PowerTrip", "BrutalSwing", "MaliciousMoonsault", "BaddyBad", "JawLock", "MaxDarkness", "Obstruct", "FalseSurrender", "LashOut", "WickedBlow", "FieryWrath", "CeaselessEdge", "KowtowCleave", "Ruination", "Comeuppance", "WickedTorque" };
                var ghostMoves = new List<string> { "NightShade", "ConfuseRay", "Lick", "Nightmare", "Curse", "Spite", "DestinyBond", "ShadowBall", "Grudge", "Astonish", "ShadowPunch", "ShadowClaw", "ShadowSneak", "OminousWind", "ShadowForce", "Hex", "PhantomForce", "Trick-or-Treat", "Never-EndingNightmare", "Never-EndingNightmare", "SpiritShackle", "SinisterArrowRaid", "Soul-Stealing7-StarStrike", "ShadowBone", "SpectralThief", "MoongeistBeam", "MenacingMoonrazeMaelstrom", "MaxPhantasm", "Poltergeist", "AstralBarrage", "BitterMalice", "InfernalParade", "LastRespects", "RageFist" };
                var poisonMoves = new List<string> { "PoisonSting", "Acid", "PoisonPowder", "Toxic", "Smog", "Sludge", "PoisonGas", "AcidArmor", "SludgeBomb", "PoisonFang", "PoisonTail", "GastroAcid", "ToxicSpikes", "PoisonJab", "CrossPoison", "GunkShot", "Venoshock", "SludgeWave", "Coil", "AcidSpray", "ClearSmog", "Belch", "VenomDrench", "AcidDownpour", "AcidDownpour", "BanefulBunker", "ToxicThread", "Purify", "MaxOoze", "ShellSideArm", "CorrosiveGas", "DireClaw", "BarbBarrage", "MortalSpin", "NoxiousTorque" };
                var iceMoves = new List<string> { "FreezeDry", "IcePunch", "Mist", "IceBeam", "Blizzard", "AuroraBeam", "Haze", "PowderSnow", "IcyWind", "Hail", "IceBall", "SheerCold", "IcicleSpear", "Avalanche", "IceShard", "IceFang", "FrostBreath", "Glaciate", "FreezeShock", "IceBurn", "IcicleCrash", "Freeze-Dry", "SubzeroSlammer", "SubzeroSlammer", "IceHammer", "AuroraVeil", "FreezyFrost", "MaxHailstorm", "TripleAxel", "GlacialLance", "MountainGale", "IceSpinner", "ChillyReception", "Snowscape" };
                var steelMoves = new List<string> { "SteelWing", "IronTail", "MetalClaw", "MeteorMash", "MetalSound", "IronDefense", "DoomDesire", "GyroBall", "MetalBurst", "BulletPunch", "MirrorShot", "FlashCannon", "IronHead", "MagnetBomb", "Autotomize", "HeavySlam", "ShiftGear", "GearGrind", "King'sShield", "CorkscrewCrash", "CorkscrewCrash", "GearUp", "AnchorShot", "SmartStrike", "SunsteelStrike", "SearingSunrazeSmash", "DoubleIronBash", "MaxSteelspike", "BehemothBlade", "BehemothBash", "SteelBeam", "SteelRoller", "Shelter", "SpinOut", "MakeItRain", "GigatonHammer" };
                var rockMoves = new List<string> { "RockThrow", "RockSlide", "Sandstorm", "Rollout", "AncientPower", "RockTomb", "RockBlast", "RockPolish", "PowerGem", "RockWrecker", "StoneEdge", "StealthRock", "HeadSmash", "WideGuard", "SmackDown", "DiamondStorm", "ContinentalCrush", "ContinentalCrush", "Accelerock", "SplinteredStormshards", "TarShot", "MaxRockfall", "MeteorBeam", "StoneAxe", "SaltCure" };
                var groundMoves = new List<string> { "MudSlap", "SandAttack", "Earthquake", "Fissure", "Dig", "BoneClub", "Bonemerang", "Mud-Slap", "Spikes", "BoneRush", "Magnitude", "MudSport", "SandTomb", "MudShot", "EarthPower", "MudBomb", "Bulldoze", "DrillRun", "Rototiller", "ThousandArrows", "ThousandWaves", "Land'sWrath", "PrecipiceBlades", "TectonicRage", "TectonicRage", "ShoreUp", "HighHorsepower", "StompingTantrum", "MaxQuake", "ScorchingSands", "HeadlongRush", "SandsearStorm" };
                var fairyMoves = new List<string> { "BabyDollEyes", "SweetKiss", "Charm", "Moonlight", "DisarmingVoice", "DrainingKiss", "CraftyShield", "FlowerShield", "MistyTerrain", "PlayRough", "FairyWind", "Moonblast", "FairyLock", "AromaticMist", "Geomancy", "DazzlingGleam", "Baby-DollEyes", "LightofRuin", "TwinkleTackle", "TwinkleTackle", "FloralHealing", "GuardianofAlola", "FleurCannon", "Nature'sMadness", "Let'sSnuggleForever", "SparklySwirl", "MaxStarfall", "Decorate", "SpiritBreak", "StrangeSteam", "MistyExplosion", "SpringtideStorm", "MagicalTorque" };
                var grassMoves = new List<string> { "VineWhip", "Absorb", "MegaDrain", "LeechSeed", "RazorLeaf", "SolarBeam", "StunSpore", "SleepPowder", "PetalDance", "Spore", "CottonSpore", "GigaDrain", "Synthesis", "Ingrain", "NeedleArm", "Aromatherapy", "GrassWhistle", "BulletSeed", "FrenzyPlant", "MagicalLeaf", "LeafBlade", "WorrySeed", "SeedBomb", "EnergyBall", "LeafStorm", "PowerWhip", "GrassKnot", "WoodHammer", "SeedFlare", "GrassPledge", "HornLeech", "LeafTornado", "CottonGuard", "Forest'sCurse", "PetalBlizzard", "GrassyTerrain", "SpikyShield", "BloomDoom", "BloomDoom", "StrengthSap", "SolarBlade", "Leafage", "TropKick", "SappySeed", "MaxOvergrowth", "DrumBeating", "SnapTrap", "BranchPoke", "AppleAcid", "GravApple", "GrassyGlide", "JungleHealing", "Chloroblast", "SpicyExtract", "FlowerTrick", "Trailblaze", "MatchaGotcha", "SyrupBomb", "IvyCudgel" };
                var fightingMoves = new List<string> { "KarateChop", "DoubleKick", "JumpKick", "RollingKick", "Submission", "LowKick", "Counter", "SeismicToss", "HighJumpKick", "TripleKick", "Reversal", "MachPunch", "Detect", "DynamicPunch", "VitalThrow", "CrossChop", "RockSmash", "FocusPunch", "Superpower", "Revenge", "BrickBreak", "ArmThrust", "SkyUppercut", "BulkUp", "Wake-UpSlap", "HammerArm", "CloseCombat", "ForcePalm", "AuraSphere", "DrainPunch", "VacuumWave", "FocusBlast", "StormThrow", "LowSweep", "QuickGuard", "CircleThrow", "FinalGambit", "SacredSword", "SecretSword", "FlyingPress", "MatBlock", "Power-UpPunch", "All-OutPummeling", "All-OutPummeling", "NoRetreat", "Octolock", "MaxKnuckle", "BodyPress", "MeteorAssault", "Coaching", "ThunderousKick", "VictoryDance", "TripleArrows", "AxeKick", "CollisionCourse", "CombatTorque" };
                var normalMoves = new List<string> { "SelfDestruct", "SoftBoiled", "LockOn", "DoubleEdge", "Pound", "DoubleSlap", "CometPunch", "MegaPunch", "PayDay", "Scratch", "ViseGrip", "Guillotine", "RazorWind", "SwordsDance", "Cut", "Whirlwind", "Bind", "Slam", "Stomp", "MegaKick", "Headbutt", "HornAttack", "FuryAttack", "HornDrill", "Tackle", "BodySlam", "Wrap", "TakeDown", "Thrash", "Double-Edge", "TailWhip", "Leer", "Growl", "Roar", "Sing", "Supersonic", "SonicBoom", "Disable", "HyperBeam", "Strength", "Growth", "QuickAttack", "Rage", "Mimic", "Screech", "DoubleTeam", "Recover", "Harden", "Minimize", "Smokescreen", "DefenseCurl", "FocusEnergy", "Bide", "Metronome", "Self-Destruct", "EggBomb", "Swift", "SkullBash", "SpikeCannon", "Constrict", "Soft-Boiled", "Glare", "Barrage", "LovelyKiss", "Transform", "DizzyPunch", "Flash", "Splash", "Explosion", "FurySwipes", "HyperFang", "Sharpen", "Conversion", "TriAttack", "SuperFang", "Slash", "Substitute", "Struggle", "Sketch", "MindReader", "Snore", "Flail", "Conversion2", "Protect", "ScaryFace", "BellyDrum", "Foresight", "PerishSong", "Lock-On", "Endure", "FalseSwipe", "Swagger", "MilkDrink", "MeanLook", "Attract", "SleepTalk", "HealBell", "Return", "Present", "Frustration", "Safeguard", "PainSplit", "BatonPass", "Encore", "RapidSpin", "SweetScent", "MorningSun", "HiddenPower", "PsychUp", "ExtremeSpeed", "FakeOut", "Uproar", "Stockpile", "SpitUp", "Swallow", "Facade", "SmellingSalts", "FollowMe", "NaturePower", "HelpingHand", "Wish", "Assist", "Recycle", "Yawn", "Endeavor", "Refresh", "SecretPower", "Camouflage", "TeeterDance", "SlackOff", "HyperVoice", "CrushClaw", "WeatherBall", "OdorSleuth", "Tickle", "Block", "Howl", "Covet", "NaturalGift", "Feint", "Acupressure", "TrumpCard", "WringOut", "LuckyChant", "MeFirst", "Copycat", "LastResort", "GigaImpact", "RockClimb", "Captivate", "Judgment", "DoubleHit", "CrushGrip", "SimpleBeam", "Entrainment", "AfterYou", "Round", "EchoedVoice", "ChipAway", "ShellSmash", "ReflectType", "Retaliate", "Bestow", "WorkUp", "TailSlap", "HeadCharge", "TechnoBlast", "RelicSong", "NobleRoar", "Boomburst", "PlayNice", "Confide", "HappyHour", "Celebrate", "HoldHands", "HoldBack", "BreakneckBlitz", "BreakneckBlitz", "Spotlight", "LaserFocus", "RevelationDance", "PulverizingPancake", "ExtremeEvoboost", "TearfulLook", "Multi-Attack", "VeeveeVolley", "MaxGuard", "StuffCheeks", "Teatime", "CourtChange", "MaxStrike", "TerrainPulse", "PowerShift", "TeraBlast", "PopulationBomb", "RevivalBlessing", "Doodle", "FilletAway", "RagingBull", "ShedTail", "TidyUp", "HyperDrill", "BloodMoon" };
                var dragonMoves = new List<string> { "DragonRage", "Outrage", "DragonBreath", "Twister", "DragonClaw", "DragonDance", "DragonPulse", "DragonRush", "DracoMeteor", "RoarofTime", "SpacialRend", "DragonTail", "DualChop", "DevastatingDrake", "DevastatingDrake", "CoreEnforcer", "ClangingScales", "DragonHammer", "ClangorousSoulblaze", "", "DynamaxCannon", "DragonDarts", "MaxWyrmwind", "ClangorousSoul", "BreakingSwipe", "Eternabeam", "ScaleShot", "DragonEnergy", "OrderUp", "GlaiveRush" };
                var flyingMoves = new List<string> { "Gust", "WingAttack", "Fly", "Peck", "DrillPeck", "MirrorMove", "SkyAttack", "Aeroblast", "FeatherDance", "AirCutter", "AerialAce", "Bounce", "Roost", "Pluck", "Tailwind", "AirSlash", "BraveBird", "Defog", "Chatter", "SkyDrop", "Acrobatics", "Hurricane", "OblivionWing", "DragonAscent", "SupersonicSkystrike", "SupersonicSkystrike", "BeakBlast", "FloatyFall", "MaxAirstream", "DualWingbeat", "BleakwindStorm" };
                var psychicMoves = new List<string> { "Psybeam", "Confusion", "Psychic", "Hypnosis", "Meditate", "Agility", "Teleport", "Barrier", "LightScreen", "Reflect", "Amnesia", "Kinesis", "DreamEater", "Psywave", "Rest", "MirrorCoat", "FutureSight", "Trick", "RolePlay", "MagicCoat", "SkillSwap", "Imprison", "LusterPurge", "MistBall", "CosmicPower", "Extrasensory", "CalmMind", "PsychoBoost", "Gravity", "MiracleEye", "HealingWish", "PsychoShift", "HealBlock", "PowerTrick", "PowerSwap", "GuardSwap", "HeartSwap", "PsychoCut", "ZenHeadbutt", "TrickRoom", "LunarDance", "GuardSplit", "PowerSplit", "WonderRoom", "Psyshock", "Telekinesis", "MagicRoom", "Synchronoise", "StoredPower", "AllySwitch", "HealPulse", "HeartStamp", "Psystrike", "HyperspaceHole", "ShatteredPsyche", "ShatteredPsyche", "PsychicTerrain", "SpeedSwap", "Instruct", "GenesisSupernova", "PsychicFangs", "PrismaticLaser", "PhotonGeyser", "LightThatBurnstheSky", "GlitzyGlow", "MagicPowder", "MaxMindstorm", "ExpandingForce", "FreezingGlare", "EerieSpell", "PsyshieldBash", "MysticalPower", "EsperWing", "LunarBlessing", "TakeHeart", "LuminaCrash", "Psyblade", "TwinBeam" };

                for (int i = 0; i < moves.Count; i++)
                {
                    foreach (var move in waterMoves)
                    {
                        var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = waterEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in fireMoves)
                    {
                        var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = fireEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in electricMoves)
                    {
                        var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = electricEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in bugEmojiMoves)
                    {
                        var regex = new Regex($@"(?<!\w){Regex.Escape(move)}\b", RegexOptions.IgnoreCase);
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = bugEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in darkMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = darkEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in ghostMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = ghostEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in poisonMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = poisonEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in iceMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = iceEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in steelMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = steelEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in rockMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = rockEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in groundMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = groundEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in fightingMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = fightingEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in dragonMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = dragonEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in flyingMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = flyingEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in psychicMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = psychicEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";

                            break;
                        }
                    }
                    foreach (var move in grassMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = grassEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in fairyMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = fairyEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                    foreach (var move in normalMoves)
                    {
                        
                        if (moves[i].Equals(move, StringComparison.OrdinalIgnoreCase))
                        {
                            moves[i] = normalEmoji + moves[i];
                            moves[i] = $"- {Regex.Replace(moves[i], "(\\p{Lu})", " $1")}";
                            break;
                        }
                    }
                }
                // Comprobar si hay movimientos que añadir al mensaje
                if (moves.Any())
                {
                    trademessage += "**Movimientos**: \n" + string.Join("\n", moves) + "\n";
                }

                trademessage += (PokeTradeBotSV.HasMark((IRibbonIndex)fin, out RibbonIndex mark) ? $"\n**Pokémon Mark**: {mark.ToString().Replace("Mark", "")}{Environment.NewLine}" : "");

                string markEntryText = "";
                var index = (int)mark - (int)RibbonIndex.MarkLunchtime;
                if (index > 0)
                    markEntryText = MarkTitle[index];

                var specitem = fin.HeldItem != 0 ? $"{SpeciesName.GetSpeciesNameGeneration(fin.Species, 2, fin.Generation <= 8 ? 8 : 9)}{TradeExtensions<T>.FormOutput(fin.Species, fin.Form, out _) + " (" + ShowdownParsing.GetShowdownText(fin).Split('@', '\n')[1].Trim() + ")"}" : $"{SpeciesName.GetSpeciesNameGeneration(fin.Species, 2, fin.Generation <= 8 ? 8 : 9) + TradeExtensions<T>.FormOutput(fin.Species, fin.Form, out _)}{markEntryText}";

                Color embedColor;

                switch (fin.Species, fin.Form)

                {
                    //gen1
                    case (1, 0): // Bulbasaur
                    case (2, 0): // Ivysaur
                    case (3, 0): // Venusaur
                        embedColor = fin.IsShiny ? new Color(197, 255, 168) : new Color(131, 204, 177); // Light Green for shiny, Green for non-shiny
                        break;
                    case (4, 0): // charmander
                        embedColor = fin.IsShiny ? new Color(254, 232, 113) : new Color(253, 154, 0); // Light Orange S, Orange NonS
                        break;
                    case (5, 0): //charmeleon
                        embedColor = fin.IsShiny ? new Color(253, 215, 130) : new Color(253, 68, 50);
                        break;
                    case (6, 0): // Charizard
                        embedColor = fin.IsShiny ? new Color(127, 127, 127) : new Color(255, 168, 0);
                        break;
                    case (7, 0): // squirtle
                        embedColor = fin.IsShiny ? new Color(181, 227, 242) : new Color(105, 197, 236);
                        break;
                    case (8, 0): // wartortle
                        embedColor = fin.IsShiny ? new Color(225, 218, 252) : new Color(186, 207, 252);
                        break;
                    case (9, 0): // blastois
                        embedColor = fin.IsShiny ? new Color(193, 184, 231) : new Color(129, 195, 237);
                        break;
                    case (10, 0): // caterpie
                        embedColor = fin.IsShiny ? new Color(236, 213, 81) : new Color(103, 195, 76);
                        break;
                    case (11, 0): // metapod
                        embedColor = fin.IsShiny ? new Color(238, 111, 44) : new Color(103, 195, 76);
                        break;
                    case (12, 0): // butterfree
                        embedColor = fin.IsShiny ? new Color(130, 116, 151) : new Color(158, 134, 186);
                        break;
                    case (13, 0): // weedle
                        embedColor = fin.IsShiny ? new Color(246, 234, 116) : new Color(239, 200, 141);
                        break;
                    case (14, 0): // kakuna
                    case (15, 0): // beedrill
                        embedColor = fin.IsShiny ? new Color(189, 253, 71) : new Color(255, 252, 0);
                        break;
                    case (16, 0): // pidgey
                        embedColor = fin.IsShiny ? new Color(198, 184, 96) : new Color(176, 113, 44);
                        break;
                    case (17, 0): // pidgeotto
                    case (18, 0): // pidgeot
                        embedColor = fin.IsShiny ? new Color(193, 184, 231) : new Color(253, 248, 192);
                        break;
                    case (19, 0): // Rattata (Kantonian Form)
                        embedColor = fin.IsShiny ? new Color(222, 238, 175) : new Color(180, 89, 192); // Light Gray for shiny, Purple for non-shiny
                        break;
                    case (20, 0): // Raticate (Kantonian Form)
                        embedColor = fin.IsShiny ? new Color(247, 113, 76) : new Color(190, 118, 0);
                        break;
                    case (21, 0): //spearow
                    case (22, 0): //fearow
                        embedColor = fin.IsShiny ? new Color(179, 177, 94) : new Color(253, 164, 0);
                        break;
                    case (23, 0): //ekans
                        embedColor = fin.IsShiny ? new Color(201, 219, 161) : new Color(203, 150, 242);
                        break;
                    case (24, 0): //arbok
                        embedColor = fin.IsShiny ? new Color(214, 185, 105) : new Color(174, 169, 207);
                        break;
                    case (25, 0): //pikachu
                        embedColor = fin.IsShiny ? new Color(255, 179, 4) : new Color(255, 235, 0);
                        break;
                    case (26, 0): // Raichu (Kantonian Form)
                        embedColor = fin.IsShiny ? new Color(242, 148, 84) : new Color(251, 186, 0);
                        break;
                    case (27, 0): // sandrew
                        embedColor = fin.IsShiny ? new Color(200, 255, 163) : new Color(255, 234, 0);
                        break;
                    case (28, 0): // sandslash
                        embedColor = fin.IsShiny ? new Color(233, 230, 153) : new Color(253, 229, 0);
                        break;
                    case (29, 0): //nidoran female
                        embedColor = fin.IsShiny ? new Color(217, 178, 231) : new Color(185, 196, 226);
                        break;
                    case (30, 0): //nidorina
                        embedColor = fin.IsShiny ? new Color(227, 171, 216) : new Color(191, 226, 248);
                        break;
                    case (31, 0): //nidoqueen
                        embedColor = fin.IsShiny ? new Color(170, 179, 134) : new Color(140, 208, 229);
                        break;
                    case (32, 0): //nidoran
                        embedColor = fin.IsShiny ? new Color(180, 205, 246) : new Color(238, 193, 248);
                        break;
                    case (33, 0): //nidorino
                        embedColor = fin.IsShiny ? new Color(172, 189, 219) : new Color(203, 161, 209);
                        break;
                    case (34, 0): //nidoking
                        embedColor = fin.IsShiny ? new Color(134, 187, 255) : new Color(219, 168, 227);
                        break;
                    case (35, 0): //clefairy
                    case (36, 0): //clefable
                        embedColor = fin.IsShiny ? new Color(248, 219, 221) : new Color(248, 203, 210);
                        break;
                    case (37, 0): //vulpix
                        embedColor = fin.IsShiny ? new Color(255, 247, 146) : new Color(252, 154, 89);
                        break;
                    case (38, 0): //ninetales
                        embedColor = fin.IsShiny ? new Color(242, 236, 246) : new Color(248, 244, 153);
                        break;
                    case (39, 0): //jigglypuff
                    case (40, 0): //wigglytuff
                        embedColor = fin.IsShiny ? new Color(223, 181, 203) : new Color(245, 199, 209);
                        break;
                    case (41, 0): //zubat
                    case (42, 0): //golbat
                        embedColor = fin.IsShiny ? new Color(115, 184, 57) : new Color(0, 199, 255);
                        break;
                    case (43, 0): //odish
                    case (44, 0): //gloom
                    case (45, 0): //vileplum
                        embedColor = fin.IsShiny ? new Color(112, 202, 88) : new Color(97, 166, 199);
                        break;
                    case (46, 0): //paras
                        embedColor = fin.IsShiny ? new Color(255, 129, 64) : new Color(255, 129, 64);
                        break;
                    case (47, 0): //parasect
                        embedColor = fin.IsShiny ? new Color(255, 255, 133) : new Color(255, 129, 64);
                        break;
                    case (48, 0): //venonat
                        embedColor = fin.IsShiny ? new Color(119, 110, 173) : new Color(135, 49, 156);
                        break;
                    case (49, 0): //venomoth
                        embedColor = fin.IsShiny ? new Color(143, 201, 255) : new Color(168, 131, 175);
                        break;
                    case (50, 0): //diglet
                    case (51, 0): //dugtrio
                        embedColor = fin.IsShiny ? new Color(218, 176, 128) : new Color(192, 150, 126);
                        break;
                    case (52, 0): //meowth
                    case (53, 0): //persian
                        embedColor = fin.IsShiny ? new Color(255, 255, 216) : new Color(254, 241, 143);
                        break;
                    case (54, 0): //psyduck
                        embedColor = fin.IsShiny ? new Color(170, 246, 244) : new Color(254, 208, 42);
                        break;
                    case (55, 0): //golduck
                        embedColor = fin.IsShiny ? new Color(106, 164, 235) : new Color(106, 164, 235);
                        break;
                    case (56, 0): //mankey
                        embedColor = fin.IsShiny ? new Color(226, 255, 201) : new Color(254, 245, 230);
                        break;
                    case (57, 0): //primeape
                        embedColor = fin.IsShiny ? new Color(240, 207, 172) : new Color(248, 232, 217);
                        break;
                    case (58, 0): //growlithe
                        embedColor = fin.IsShiny ? new Color(245, 216, 96) : new Color(251, 173, 90);
                        break;
                    case (59, 0): //arcanine
                        embedColor = fin.IsShiny ? new Color(251, 219, 72) : new Color(251, 173, 90);
                        break;
                    case (60, 0): //poliwag
                    case (61, 0): //poliwhirl
                        embedColor = fin.IsShiny ? new Color(147, 210, 253) : new Color(116, 177, 231);
                        break;
                    case (62, 0): //poliwrath
                        embedColor = fin.IsShiny ? new Color(133, 179, 115) : new Color(116, 177, 231);
                        break;
                    case (63, 0): //abra
                    case (64, 0): //kadabra
                        embedColor = fin.IsShiny ? new Color(255, 255, 163) : new Color(254, 215, 0);
                        break;
                    case (65, 0): //alakazam
                        embedColor = fin.IsShiny ? new Color(255, 232, 74) : new Color(254, 215, 0);
                        break;
                    case (66, 0): //machop
                        embedColor = fin.IsShiny ? new Color(217, 212, 147) : new Color(181, 220, 235);
                        break;
                    case (67, 0): //machoke
                        embedColor = fin.IsShiny ? new Color(180, 220, 149) : new Color(211, 220, 251);
                        break;
                    case (68, 0): //machamp
                        embedColor = fin.IsShiny ? new Color(189, 234, 107) : new Color(166, 202, 224);
                        break;
                    case (69, 0): //bellsprout
                        embedColor = fin.IsShiny ? new Color(255, 255, 123) : new Color(254, 255, 31);
                        break;
                    case (70, 0): //weepinbell
                        embedColor = fin.IsShiny ? new Color(220, 236, 101) : new Color(254, 255, 31);
                        break;
                    case (71, 0): //victrebel
                        embedColor = fin.IsShiny ? new Color(196, 235, 118) : new Color(254, 255, 31);
                        break;
                    case (72, 0): //tentacool
                        embedColor = fin.IsShiny ? new Color(215, 203, 253) : new Color(55, 210, 254);
                        break;
                    case (73, 0): //tentacruel
                        embedColor = fin.IsShiny ? new Color(215, 203, 253) : new Color(55, 210, 254);
                        break;
                    case (74, 0): //geodude
                        embedColor = fin.IsShiny ? new Color(205, 171, 61) : new Color(168, 170, 157);
                        break;
                    case (75, 0): //graveler
                        embedColor = fin.IsShiny ? new Color(162, 121, 39) : new Color(168, 170, 157);
                        break;
                    case (76, 0): //golem
                        embedColor = fin.IsShiny ? new Color(158, 127, 81) : new Color(168, 170, 157);
                        break;
                    case (77, 0): //ponyta
                        embedColor = fin.IsShiny ? new Color(27, 187, 221) : new Color(248, 141, 0);
                        break;
                    case (78, 0): //rapidash
                        embedColor = fin.IsShiny ? new Color(128, 123, 181) : new Color(248, 141, 0);
                        break;
                    case (79, 0): //slowpoke
                        embedColor = fin.IsShiny ? new Color(240, 214, 223) : new Color(255, 189, 203);
                        break;
                    case (80, 0): //slowbro
                        embedColor = fin.IsShiny ? new Color(205, 190, 255) : new Color(255, 189, 203);
                        break;
                    case (81, 0): //magnemite
                        embedColor = fin.IsShiny ? new Color(194, 192, 151) : new Color(162, 198, 210);
                        break;
                    case (82, 0): //magneton
                        embedColor = fin.IsShiny ? new Color(194, 192, 151) : new Color(162, 198, 210);
                        break;
                    case (83, 0): //farfetch
                        embedColor = fin.IsShiny ? new Color(255, 189, 173) : new Color(214, 179, 157);
                        break;
                    case (84, 0): //duduo
                    case (85, 0): //dodrio
                        embedColor = fin.IsShiny ? new Color(203, 231, 120) : new Color(225, 125, 13);
                        break;
                    case (86, 0): //seel
                    case (87, 0): //dewgong
                        embedColor = fin.IsShiny ? new Color(252, 244, 223) : new Color(224, 232, 243);
                        break;
                    case (88, 0): //grimer
                    case (89, 0): //muk
                        embedColor = fin.IsShiny ? new Color(130, 159, 137) : new Color(115, 111, 170);
                        break;
                    case (90, 0): //shellder
                        embedColor = fin.IsShiny ? new Color(251, 180, 90) : new Color(98, 91, 205);
                        break;
                    case (91, 0): //cloyster
                        embedColor = fin.IsShiny ? new Color(78, 112, 173) : new Color(152, 152, 202);
                        break;
                    case (92, 0): //gastly
                        embedColor = fin.IsShiny ? new Color(116, 41, 123) : new Color(53, 57, 69);
                        break;
                    case (93, 0): //haunter
                        embedColor = fin.IsShiny ? new Color(154, 140, 199) : new Color(108, 95, 151);
                        break;
                    case (94, 0): //gengar
                        embedColor = fin.IsShiny ? new Color(165, 162, 191) : new Color(108, 95, 151);
                        break;
                    case (95, 0): //onix
                        embedColor = fin.IsShiny ? new Color(158, 165, 70) : new Color(166, 170, 173);
                        break;
                    case (96, 0): //drowzee
                        embedColor = fin.IsShiny ? new Color(247, 210, 215) : new Color(253, 236, 26);
                        break;
                    case (97, 0): //hypno
                        embedColor = fin.IsShiny ? new Color(239, 148, 181) : new Color(253, 236, 26);
                        break;
                    case (98, 0): //kraby
                        embedColor = fin.IsShiny ? new Color(130, 159, 137) : new Color(254, 115, 12);
                        break;
                    case (99, 0): //kingler
                        embedColor = fin.IsShiny ? new Color(147, 170, 128) : new Color(254, 115, 12);
                        break;
                    case (100, 0): //voltorb
                    case (101, 0): //electrode
                        embedColor = fin.IsShiny ? new Color(89, 157, 254) : new Color(254, 55, 88);
                        break;
                    case (102, 0): //exeggcute
                        embedColor = fin.IsShiny ? new Color(236, 184, 100) : new Color(239, 189, 198);
                        break;
                    case (103, 0): //Exeggutor
                        embedColor = fin.IsShiny ? new Color(206, 199, 93) : new Color(194, 148, 112);
                        break;
                    case (104, 0): //cubone
                        embedColor = fin.IsShiny ? new Color(122, 150, 89) : new Color(215, 162, 94);
                        break;
                    case (105, 0): //marowak
                        embedColor = fin.IsShiny ? new Color(174, 215, 95) : new Color(215, 162, 94);
                        break;
                    case (106, 0): //hitmonlee
                        embedColor = fin.IsShiny ? new Color(156, 185, 118) : new Color(186, 136, 109);
                        break;
                    case (107, 0): //hitmonchan
                        embedColor = fin.IsShiny ? new Color(162, 167, 109) : new Color(215, 175, 105);
                        break;
                    case (108, 0): //lickitung
                        embedColor = fin.IsShiny ? new Color(224, 199, 109) : new Color(238, 153, 172);
                        break;
                    case (109, 0): //koffing
                        embedColor = fin.IsShiny ? new Color(164, 195, 198) : new Color(170, 171, 238);
                        break;
                    case (110, 0): //weezing
                        embedColor = fin.IsShiny ? new Color(126, 173, 165) : new Color(179, 158, 189);
                        break;
                    case (111, 0): //ryhorn
                        embedColor = fin.IsShiny ? new Color(204, 149, 118) : new Color(166, 187, 214);
                        break;
                    case (112, 0): //rydon
                        embedColor = fin.IsShiny ? new Color(204, 149, 118) : new Color(166, 187, 214);
                        break;
                    case (113, 0): //chansey
                        embedColor = fin.IsShiny ? new Color(247, 237, 202) : new Color(252, 197, 220);
                        break;
                    case (114, 0): //tangela
                        embedColor = fin.IsShiny ? new Color(93, 180, 65) : new Color(63, 161, 248);
                        break;
                    case (115, 0): //kangaskan
                        embedColor = fin.IsShiny ? new Color(196, 186, 177) : new Color(200, 145, 104);
                        break;
                    case (116, 0): //horsea
                        embedColor = fin.IsShiny ? new Color(74, 191, 182) : new Color(57, 194, 248);
                        break;
                    case (117, 0): //seadra
                        embedColor = fin.IsShiny ? new Color(156, 171, 228) : new Color(57, 194, 248);
                        break;
                    case (118, 0): //goldeen
                        embedColor = fin.IsShiny ? new Color(252, 190, 107) : new Color(253, 147, 97);
                        break;
                    case (119, 0): //seaking
                        embedColor = fin.IsShiny ? new Color(252, 190, 107) : new Color(253, 147, 97);
                        break;
                    case (120, 0): //staryu
                        embedColor = fin.IsShiny ? new Color(229, 244, 215) : new Color(195, 125, 12);
                        break;
                    case (121, 0): //starmie
                        embedColor = fin.IsShiny ? new Color(116, 148, 195) : new Color(146, 147, 214);
                        break;
                    case (122, 0): //mr. mime
                        embedColor = fin.IsShiny ? new Color(169, 233, 113) : new Color(252, 125, 152);
                        break;
                    case (123, 0): //scyhter
                        embedColor = fin.IsShiny ? new Color(138, 215, 133) : new Color(111, 207, 81);
                        break;
                    case (124, 0): //jynx
                        embedColor = fin.IsShiny ? new Color(224, 113, 179) : new Color(225, 34, 42);
                        break;
                    case (125, 0): //electrabuzz
                        embedColor = fin.IsShiny ? new Color(253, 171, 123) : new Color(253, 229, 0);
                        break;
                    case (126, 0): //magmar
                        embedColor = fin.IsShiny ? new Color(248, 133, 136) : new Color(255, 46, 65);
                        break;
                    case (127, 0): //pinsir
                        embedColor = fin.IsShiny ? new Color(162, 160, 197) : new Color(187, 148, 131);
                        break;
                    case (128, 0): //tauros
                        embedColor = fin.IsShiny ? new Color(216, 221, 118) : new Color(198, 121, 33);
                        break;
                    case (129, 0): //magikarp
                        embedColor = fin.IsShiny ? new Color(230, 192, 95) : new Color(251, 83, 12);
                        break;
                    case (130, 0): //gyrados
                        embedColor = fin.IsShiny ? new Color(196, 60, 72) : new Color(51, 187, 245);
                        break;
                    case (131, 0): //lapras
                        embedColor = fin.IsShiny ? new Color(163, 130, 221) : new Color(82, 167, 211);
                        break;
                    case (132, 0): //ditto
                        embedColor = fin.IsShiny ? new Color(132, 200, 239) : new Color(187, 165, 240);
                        break;
                    case (133, 0): //eevee
                        embedColor = fin.IsShiny ? new Color(255, 250, 228) : new Color(165, 110, 43);
                        break;
                    case (134, 0): //vaporean
                        embedColor = fin.IsShiny ? new Color(190, 142, 202) : new Color(53, 198, 225);
                        break;
                    case (135, 0): //joltean
                        embedColor = fin.IsShiny ? new Color(198, 231, 116) : new Color(255, 219, 0);
                        break;
                    case (136, 0): //flareon
                        embedColor = fin.IsShiny ? new Color(223, 156, 52) : new Color(231, 128, 57);
                        break;
                    case (137, 0): //poygon
                        embedColor = fin.IsShiny ? new Color(76, 95, 197) : new Color(255, 145, 172);
                        break;
                    case (138, 0): //omanyte
                        embedColor = fin.IsShiny ? new Color(213, 156, 227) : new Color(0, 172, 209);
                        break;
                    case (139, 0): //omaster
                        embedColor = fin.IsShiny ? new Color(213, 156, 227) : new Color(0, 172, 209);
                        break;
                    case (140, 0): //kabuto
                        embedColor = fin.IsShiny ? new Color(114, 204, 104) : new Color(185, 116, 0);
                        break;
                    case (141, 0): //kabutops
                        embedColor = fin.IsShiny ? new Color(198, 245, 79) : new Color(185, 116, 0);
                        break;
                    case (142, 0): //aerodactly
                        embedColor = fin.IsShiny ? new Color(252, 195, 250) : new Color(208, 191, 235);
                        break;
                    case (143, 0): //snorlax
                        embedColor = fin.IsShiny ? new Color(22, 77, 160) : new Color(0, 114, 151);
                        break;
                    case (144, 0): //articuno
                        embedColor = fin.IsShiny ? new Color(147, 180, 211) : new Color(76, 157, 200);
                        break;
                    case (145, 0): //zapdos
                        embedColor = fin.IsShiny ? new Color(250, 208, 70) : new Color(250, 208, 70);
                        break;
                    case (146, 0): //moltres
                        embedColor = fin.IsShiny ? new Color(252, 157, 187) : new Color(249, 203, 0);
                        break;
                    case (147, 0): //dratini
                        embedColor = fin.IsShiny ? new Color(241, 176, 232) : new Color(146, 162, 213);
                        break;
                    case (148, 0): //dragonair
                        embedColor = fin.IsShiny ? new Color(254, 158, 230) : new Color(0, 187, 254);
                        break;
                    case (149, 0): //dragonite
                        embedColor = fin.IsShiny ? new Color(112, 151, 107) : new Color(252, 186, 1);
                        break;
                    case (150, 0): //mewtwo
                        embedColor = fin.IsShiny ? new Color(229, 229, 229) : new Color(210, 191, 213);
                        break;
                    case (151, 0): //mew
                        embedColor = fin.IsShiny ? new Color(191, 245, 255) : new Color(255, 243, 247);
                        break;

                    //gen2
                    case (243, 0): // raikou
                        embedColor = fin.IsShiny ? new Color(239, 222, 152) : new Color(246, 204, 82);
                        break;
                    case (244, 0): // entei
                        embedColor = fin.IsShiny ? new Color(162, 109, 69) : new Color(162, 109, 69);
                        break;
                    case (245, 0): // suicune
                        embedColor = fin.IsShiny ? new Color(115, 173, 193) : new Color(84, 132, 134);
                        break;

                    //gen3
                    case (382, 0): // kyogre
                        embedColor = fin.IsShiny ? new Color(218, 80, 228) : new Color(1, 109, 181);
                        break;
                    case (383, 0): // groudon
                        embedColor = fin.IsShiny ? new Color(234, 242, 60) : new Color(205, 58, 38);
                        break;
                    case (384, 0): // rayquaza
                        embedColor = fin.IsShiny ? new Color(76, 86, 77) : new Color(67, 139, 102);
                        break;


                    //gen8
                    case (52, 2): // meowth galar
                        embedColor = fin.IsShiny ? new Color(239, 222, 152) : new Color(190, 174, 158);
                        break;
                    case (58, 1): // growlithe hisui
                    case (59, 1): //arcanine hisui
                        embedColor = fin.IsShiny ? new Color(208, 177, 50) : new Color(220, 86, 51);
                        break;
                    case (77, 1): // ponyta hisui
                    case (78, 1): // rapidash
                        embedColor = fin.IsShiny ? new Color(242, 242, 244) : new Color(242, 242, 244);
                        break;
                    case (79, 1): // slowpoke galar
                        embedColor = fin.IsShiny ? new Color(255, 252, 0) : new Color(255, 179, 231);
                        break;
                    case (80, 2): // slowbro galar
                        embedColor = fin.IsShiny ? new Color(245, 201, 166) : new Color(243, 163, 212);
                        break;
                    case (199, 1): // slowking galar
                        embedColor = fin.IsShiny ? new Color(217, 117, 169) : new Color(243, 163, 212);
                        break;
                    case (83, 1): // farfetch d galar
                        embedColor = fin.IsShiny ? new Color(122, 103, 107) : new Color(93, 79, 76);
                        break;
                    case (100, 1): // voltrob hisui
                    case (101, 1): // electrode hisui
                        embedColor = fin.IsShiny ? new Color(97, 95, 96) : new Color(224, 96, 25);
                        break;
                    case (110, 1): // weezing galar
                        embedColor = fin.IsShiny ? new Color(179, 146, 111) : new Color(162, 166, 169);
                        break;
                    case (122, 1): // mr mime galar
                        embedColor = fin.IsShiny ? new Color(173, 193, 218) : new Color(139, 183, 246);
                        break;
                    case (144, 1): // articuno galar
                        embedColor = fin.IsShiny ? new Color(112, 202, 239) : new Color(184, 156, 194);
                        break;
                    case (145, 2): // zapdos galar
                        embedColor = fin.IsShiny ? new Color(253, 236, 94) : new Color(236, 104, 63);
                        break;
                    case (157, 1): // tyhplosion hisui
                        embedColor = fin.IsShiny ? new Color(62, 113, 158) : new Color(79, 65, 116);
                        break;
                    case (483, 1): // dialga origin
                        embedColor = fin.IsShiny ? new Color(3, 151, 177) : new Color(49, 94, 143);
                        break;
                    case (484, 1): // palkia origin
                        embedColor = fin.IsShiny ? new Color(248, 223, 242) : new Color(216, 198, 220);
                        break;
                    case (503, 1): // samurot hisui
                        embedColor = fin.IsShiny ? new Color(0, 81, 145) : new Color(0, 81, 145);
                        break;
                    case (888, 0): // zacian
                        embedColor = fin.IsShiny ? new Color(107, 193, 228) : new Color(70, 135, 193);
                        break;
                    case (889, 0): // zamamenta 
                        embedColor = fin.IsShiny ? new Color(232, 101, 181) : new Color(198, 68, 75);
                        break;
                    case (890, 0): // elternatus
                        embedColor = fin.IsShiny ? new Color(170, 0, 70) : new Color(31, 45, 94);
                        break;
                    case (891, 0): // kubfu
                        embedColor = fin.IsShiny ? new Color(199, 185, 172) : new Color(199, 185, 172);
                        break;
                    case (892, 0): // urshifu single
                    case (892, 1): // urshifu rapid
                        embedColor = fin.IsShiny ? new Color(113, 116, 121) : new Color(113, 116, 121);
                        break;





                    //gen9
                    case (128, 1): // Tauros Paldea Combat (386 is the national dex number)
                    case (128, 3): // Tauros Paldea Aqua (386 is the national dex number)
                    case (128, 2): // Tauros Paldea Blaze (386 is the national dex number)
                        embedColor = fin.IsShiny ? new Color(70, 70, 70) : new Color(70, 70, 70); // black color
                        break;
                    case (194, 1): //wooper paldean
                    case (980, 0): //clodsire
                        embedColor = fin.IsShiny ? new Color(176, 173, 218) : new Color(176, 173, 218);
                        break;
                    case (906, 0): //sprigatito
                    case (907, 0): //floragato
                    case (908, 0): //meowscrada
                        embedColor = fin.IsShiny ? new Color(190, 219, 171) : new Color(189, 218, 170);
                        break;
                    case (909, 0): //feucoco
                    case (910, 0): // crocolar
                        embedColor = fin.IsShiny ? new Color(217, 125, 150) : new Color(224, 87, 71);
                        break;
                    case (911, 0): //skeledirge
                        embedColor = fin.IsShiny ? new Color(217, 32, 151) : new Color(224, 87, 71);
                        break;
                    case (912, 0): //quaxly
                        embedColor = fin.IsShiny ? new Color(143, 207, 199) : new Color(60, 186, 201);
                        break;
                    case (913, 0): //quaxwell
                        embedColor = fin.IsShiny ? new Color(143, 207, 199) : new Color(21, 119, 206);
                        break;
                    case (914, 0): //quaquaval
                        embedColor = fin.IsShiny ? new Color(149, 155, 229) : new Color(56, 67, 235);
                        break;
                    case (915, 0): //lenchok
                        embedColor = fin.IsShiny ? new Color(248, 160, 198) : new Color(89, 98, 103);
                        break;
                    case (916, 1): //oinkologne f
                        embedColor = fin.IsShiny ? new Color(248, 160, 198) : new Color(89, 98, 103);
                        break;
                    case (916, 0): //oinkologne m
                        embedColor = fin.IsShiny ? new Color(248, 160, 198) : new Color(90, 69, 74);
                        break;
                    case (917, 0): //torountula
                        embedColor = fin.IsShiny ? new Color(255, 110, 94) : new Color(245, 245, 245);
                        break;
                    case (918, 0): //spidops
                        embedColor = fin.IsShiny ? new Color(151, 75, 113) : new Color(107, 150, 41);
                        break;
                    case (919, 0): //lokix
                        embedColor = fin.IsShiny ? new Color(138, 130, 57) : new Color(104, 98, 96);
                        break;
                    case (921, 0): //pawmi
                        embedColor = fin.IsShiny ? new Color(255, 115, 121) : new Color(233, 138, 36);
                        break;
                    case (922, 0): //pawmo
                        embedColor = fin.IsShiny ? new Color(254, 174, 177) : new Color(255, 192, 96);
                        break;
                    case (923, 0): //pawmot
                        embedColor = fin.IsShiny ? new Color(255, 129, 132) : new Color(232, 155, 23);
                        break;
                    case (924, 0): //tandemous
                    case (925, 0): //moushold
                    case (925, 1): //moushold 4family
                        embedColor = fin.IsShiny ? new Color(253, 253, 253) : new Color(253, 253, 253);
                        break;
                    case (926, 0): //fidough
                        embedColor = fin.IsShiny ? new Color(214, 173, 141) : new Color(253, 245, 224);
                        break;
                    case (927, 0): //dashsbun
                        embedColor = fin.IsShiny ? new Color(175, 110, 82) : new Color(194, 92, 44);
                        break;
                    case (928, 0): //smoliv
                        embedColor = fin.IsShiny ? new Color(255, 250, 104) : new Color(191, 207, 96);
                        break;
                    case (929, 0): //dolliv
                    case (930, 0): //arboliva
                        embedColor = fin.IsShiny ? new Color(97, 170, 91) : new Color(82, 163, 52);
                        break;
                    case (931, 0): //Squawkabilly green
                        embedColor = fin.IsShiny ? new Color(121, 209, 73) : new Color(121, 209, 73);
                        break;
                    case (931, 1): //Squawkabilly blue
                        embedColor = fin.IsShiny ? new Color(30, 141, 194) : new Color(30, 141, 194);
                        break;
                    case (931, 2): //Squawkabilly yellow
                        embedColor = fin.IsShiny ? new Color(241, 202, 71) : new Color(241, 202, 71);
                        break;
                    case (931, 3): //Squawkabilly white
                        embedColor = fin.IsShiny ? new Color(205, 204, 208) : new Color(205, 204, 208);
                        break;
                    case (932, 0): //nacli
                    case (933, 0): //nasstack
                    case (934, 0): //garganacl
                        embedColor = fin.IsShiny ? new Color(255, 212, 144) : new Color(250, 249, 247);
                        break;
                    case (935, 0): //charcadet
                    case (936, 0): //armourage
                        embedColor = fin.IsShiny ? new Color(237, 37, 14) : new Color(237, 37, 14);
                        break;
                    case (937, 0): //cereludge
                        embedColor = fin.IsShiny ? new Color(58, 42, 104) : new Color(58, 42, 104);
                        break;
                    case (938, 0): //tadbulb
                        embedColor = fin.IsShiny ? new Color(253, 255, 0) : new Color(253, 255, 0);
                        break;
                    case (939, 0): //bellibolt
                        embedColor = fin.IsShiny ? new Color(190, 192, 20) : new Color(33, 170, 142);
                        break;
                    case (940, 0): //wattrel
                        embedColor = fin.IsShiny ? new Color(102, 61, 77) : new Color(63, 66, 75);
                        break;
                    case (941, 0): //kilowattrel
                        embedColor = fin.IsShiny ? new Color(84, 43, 59) : new Color(80, 78, 81);
                        break;
                    case (942, 0): //Maschiff
                        embedColor = fin.IsShiny ? new Color(150, 136, 171) : new Color(80, 78, 81);
                        break;
                    case (943, 0): //Mabosstiff
                        embedColor = fin.IsShiny ? new Color(110, 98, 122) : new Color(130, 116, 103);
                        break;
                    case (944, 0): //Shroodle
                        embedColor = fin.IsShiny ? new Color(71, 67, 64) : new Color(84, 69, 64);
                        break;
                    case (945, 0): //Grafaiai
                        embedColor = fin.IsShiny ? new Color(119, 119, 81) : new Color(63, 59, 76);
                        break;
                    case (946, 0): //Bramblin
                        embedColor = fin.IsShiny ? new Color(253, 247, 187) : new Color(223, 194, 138);
                        break;
                    case (947, 0): //Brambleghast
                        embedColor = fin.IsShiny ? new Color(230, 224, 176) : new Color(193, 169, 109);
                        break;
                    case (948, 0): //Toedscool
                        embedColor = fin.IsShiny ? new Color(225, 220, 200) : new Color(242, 197, 176);
                        break;
                    case (949, 0): //Toedscruel
                        embedColor = fin.IsShiny ? new Color(183, 87, 99) : new Color(242, 240, 189);
                        break;
                    case (950, 0): //Klawf
                        embedColor = fin.IsShiny ? new Color(53, 165, 249) : new Color(234, 122, 58);
                        break;
                    case (951, 0): //Capsakid
                        embedColor = fin.IsShiny ? new Color(252, 245, 128) : new Color(116, 175, 119);
                        break;
                    case (952, 0): //Scovillain
                        embedColor = fin.IsShiny ? new Color(58, 110, 134) : new Color(62, 107, 78);
                        break;
                    case (953, 0): //Rellor
                        embedColor = fin.IsShiny ? new Color(203, 169, 1) : new Color(133, 108, 68);
                        break;
                    case (954, 0): //Rabsca
                        embedColor = fin.IsShiny ? new Color(227, 230, 29) : new Color(95, 185, 193);
                        break;
                    case (955, 0): //Flittle
                        embedColor = fin.IsShiny ? new Color(254, 250, 143) : new Color(254, 250, 143);
                        break;
                    case (956, 0): //Espathra
                        embedColor = fin.IsShiny ? new Color(143, 110, 105) : new Color(247, 188, 84);
                        break;
                    case (957, 0): //Tinkatink
                    case (958, 0): //Tinkatuff
                    case (959, 0): //Tinkaton
                        embedColor = fin.IsShiny ? new Color(251, 93, 170) : new Color(251, 93, 170);
                        break;
                    case (960, 0): //Wiglett
                        embedColor = fin.IsShiny ? new Color(255, 189, 51) : new Color(245, 244, 242);
                        break;
                    case (961, 0): //Wugtrio
                        embedColor = fin.IsShiny ? new Color(90, 89, 219) : new Color(233, 83, 108);
                        break;
                    case (962, 0): //Bombirdier
                        embedColor = fin.IsShiny ? new Color(241, 229, 231) : new Color(241, 229, 231);
                        break;
                    case (963, 0): //Finizen
                    case (964, 0): //Palafin
                        embedColor = fin.IsShiny ? new Color(125, 128, 223) : new Color(110, 193, 209);
                        break;
                    case (964, 1): //Palafin Hero Form
                        embedColor = fin.IsShiny ? new Color(81, 64, 96) : new Color(22, 100, 175);
                        break;
                    case (965, 0): //Varoom
                    case (966, 0): //Revavroom
                        embedColor = fin.IsShiny ? new Color(114, 96, 118) : new Color(114, 96, 118);
                        break;
                    case (967, 0): //Cyclizar
                        embedColor = fin.IsShiny ? new Color(131, 128, 97) : new Color(127, 167, 133);
                        break;
                    case (968, 0): //Orthworm
                        embedColor = fin.IsShiny ? new Color(143, 209, 223) : new Color(229, 95, 84);
                        break;
                    case (969, 0): //Glimmet
                        embedColor = fin.IsShiny ? new Color(96, 147, 236) : new Color(130, 130, 190);
                        break;
                    case (970, 0): //Glimmora
                        embedColor = fin.IsShiny ? new Color(0, 117, 143) : new Color(41, 78, 159);
                        break;
                    case (971, 0): //Greavard
                        embedColor = fin.IsShiny ? new Color(204, 182, 83) : new Color(137, 148, 166);
                        break;
                    case (972, 0): //Houndstone
                        embedColor = fin.IsShiny ? new Color(185, 177, 140) : new Color(176, 176, 202);
                        break;
                    case (973, 0): //Flamigo
                        embedColor = fin.IsShiny ? new Color(255, 228, 233) : new Color(222, 107, 136);
                        break;
                    case (974, 0): //Cetoddle
                        embedColor = fin.IsShiny ? new Color(118, 127, 134) : new Color(248, 247, 243);
                        break;
                    case (975, 0): //Cetitan
                        embedColor = fin.IsShiny ? new Color(67, 84, 78) : new Color(251, 251, 249);
                        break;
                    case (976, 0): //Veluza
                        embedColor = fin.IsShiny ? new Color(176, 176, 176) : new Color(176, 176, 176);
                        break;
                    case (977, 0): //Dondozo
                        embedColor = fin.IsShiny ? new Color(226, 205, 80) : new Color(53, 164, 220);
                        break;
                    case (978, 0): //Tatsugiri curly form
                        embedColor = fin.IsShiny ? new Color(255, 139, 99) : new Color(241, 162, 129);
                        break;
                    case (978, 1): //Tatsugiri droopy form
                        embedColor = fin.IsShiny ? new Color(245, 245, 245) : new Color(231, 75, 115);
                        break;
                    case (978, 2): //Tatsugiri stretchy form
                        embedColor = fin.IsShiny ? new Color(253, 123, 63) : new Color(243, 212, 36);
                        break;
                    case (979, 0): //Annihilape
                        embedColor = fin.IsShiny ? new Color(186, 194, 243) : new Color(168, 168, 180);
                        break;
                    case (981, 0): //Farigiraf
                        embedColor = fin.IsShiny ? new Color(243, 89, 81) : new Color(241, 141, 79);
                        break;
                    case (982, 0): //Dundunsparce 2
                    case (982, 1): //Dundunsparce 3
                        embedColor = fin.IsShiny ? new Color(255, 252, 119) : new Color(255, 252, 119);
                        break;
                    case (983, 0): //Kingambit
                        embedColor = fin.IsShiny ? new Color(51, 60, 181) : new Color(140, 63, 57);
                        break;
                    case (984, 0): //Great Tusk
                        embedColor = fin.IsShiny ? new Color(143, 81, 58) : new Color(58, 59, 87);
                        break;
                    case (985, 0): //Scream Tail
                        embedColor = fin.IsShiny ? new Color(239, 201, 238) : new Color(239, 201, 238);
                        break;
                    case (986, 0): //Brute Bonnet
                        embedColor = fin.IsShiny ? new Color(85, 76, 231) : new Color(204, 54, 56);
                        break;
                    case (987, 0): //Flutter Mane
                        embedColor = fin.IsShiny ? new Color(127, 168, 90) : new Color(59, 121, 136);
                        break;
                    case (988, 0): //Slither Wing
                        embedColor = fin.IsShiny ? new Color(250, 207, 95) : new Color(235, 90, 69);
                        break;
                    case (989, 0): //Sandy Shocks
                        embedColor = fin.IsShiny ? new Color(87, 87, 87) : new Color(215, 216, 218);
                        break;
                    case (990, 0): //Iron Threads
                        embedColor = fin.IsShiny ? new Color(161, 166, 169) : new Color(161, 166, 169);
                        break;
                    case (991, 0): //Iron Bundle
                        embedColor = fin.IsShiny ? new Color(180, 180, 180) : new Color(203, 67, 55);
                        break;
                    case (992, 0): //Iron hands
                        embedColor = fin.IsShiny ? new Color(208, 204, 201) : new Color(62, 75, 109);
                        break;
                    case (993, 0): //iron jugulis
                        embedColor = fin.IsShiny ? new Color(195, 195, 195) : new Color(47, 41, 45);
                        break;
                    case (994, 0): //iron moth
                        embedColor = fin.IsShiny ? new Color(138, 138, 138) : new Color(235, 234, 232);
                        break;
                    case (995, 0): //iron thorns
                        embedColor = fin.IsShiny ? new Color(180, 169, 173) : new Color(131, 181, 60);
                        break;
                    case (996, 0): //frigibax
                        embedColor = fin.IsShiny ? new Color(148, 177, 181) : new Color(156, 163, 173);
                        break;
                    case (997, 0): //arctibax
                        embedColor = fin.IsShiny ? new Color(114, 189, 185) : new Color(106, 141, 160);
                        break;
                    case (998, 0): //baxcalibur
                        embedColor = fin.IsShiny ? new Color(64, 111, 117) : new Color(87, 104, 122);
                        break;
                    case (999, 0): //gimmighoul
                        embedColor = fin.IsShiny ? new Color(255, 231, 138) : new Color(255, 231, 138);
                        break;
                    case (999, 1): //gimmighoul roaming
                        embedColor = fin.IsShiny ? new Color(246, 249, 254) : new Color(157, 167, 194);
                        break;
                    case (1000, 0): //gholdengo
                        embedColor = fin.IsShiny ? new Color(255, 218, 55) : new Color(255, 218, 55);
                        break;
                    case (1001, 0): //wochien
                        embedColor = fin.IsShiny ? new Color(59, 65, 53) : new Color(85, 90, 83);
                        break;
                    case (1002, 0): //chienpao
                        embedColor = fin.IsShiny ? new Color(104, 90, 77) : new Color(253, 253, 253);
                        break;
                    case (1003, 0): //tinglu
                        embedColor = fin.IsShiny ? new Color(123, 117, 91) : new Color(112, 87, 67);
                        break;
                    case (1004, 0): //chiyu
                        embedColor = fin.IsShiny ? new Color(204, 253, 255) : new Color(238, 181, 112);
                        break;
                    case (1005, 0): //roaring moon
                        embedColor = fin.IsShiny ? new Color(125, 162, 131) : new Color(74, 169, 199);
                        break;
                    case (1006, 0): //iron valiant
                        embedColor = fin.IsShiny ? new Color(182, 182, 182) : new Color(110, 171, 94);
                        break;
                    case (1007, 0): //koraidon
                        embedColor = new Color(233, 73, 61);
                        break;
                    case (1008, 0): //miraidon
                        embedColor = new Color(55, 51, 136);
                        break;
                    case (1009, 0): //walking wake
                        embedColor = fin.IsShiny ? new Color(92, 207, 202) : new Color(76, 129, 143);
                        break;
                    case (1010, 0): //iron leaves
                        embedColor = fin.IsShiny ? new Color(229, 231, 230) : new Color(85, 170, 89);
                        break;
                    case (1011, 0): //Dipplin
                        embedColor = fin.IsShiny ? new Color(255, 219, 22) : new Color(173, 13, 0);
                        break;
                    case (1012, 0): //Poltchageist
                    case (1012, 1): //Poltchageist Masterpiece
                        embedColor = fin.IsShiny ? new Color(46, 93, 29) : new Color(56, 50, 56);
                        break;
                    case (1013, 0): //Sinistcha
                    case (1013, 1): //Sinistcha Masterpiece
                        embedColor = fin.IsShiny ? new Color(27, 66, 13) : new Color(46, 38, 46);
                        break;
                    case (1014, 0): //Okidogi
                        embedColor = fin.IsShiny ? new Color(209, 124, 76) : new Color(54, 54, 54);
                        break;
                    case (1015, 0): //Munkidori
                        embedColor = fin.IsShiny ? new Color(129, 114, 101) : new Color(50, 50, 50);
                        break;
                    case (1016, 0): //Fezandipiti
                        embedColor = fin.IsShiny ? new Color(50, 50, 122) : new Color(56, 56, 56);
                        break;
                    case (1017, 0): //Ogerpon
                        embedColor = fin.IsShiny ? new Color(255, 167, 0) : new Color(58, 197, 0);
                        break;
                    case (1017, 1): //Ogerpon Wellspring Mask
                        embedColor = fin.IsShiny ? new Color(53, 190, 226) : new Color(53, 190, 226);
                        break;
                    case (1017, 2): //Ogerpon Hearthflame Mask
                        embedColor = fin.IsShiny ? new Color(139, 28, 34) : new Color(139, 28, 34);
                        break;
                    case (1017, 3): //Ogerpon Cornerstone Mask
                        embedColor = fin.IsShiny ? new Color(47, 47, 47) : new Color(47, 47, 47);
                        break;


                    // Alolan Forms
                    case (19, 1): // Rattata (Alolan Form)
                    case (20, 1): // Raticate (Alolan Form)
                        embedColor = fin.IsShiny ? new Color(165, 75, 85) : new Color(96, 102, 92);
                        break;
                    case (26, 1): // Raichu (Alolan Form)
                        embedColor = fin.IsShiny ? new Color(204, 139, 57) : new Color(174, 110, 65);
                        break;

                    default:
                        embedColor = fin.IsShiny ? Color.Gold : Color.Teal; // Gold for shiny, Teal for non-shiny
                        break;
                }

                var msg = "Mostrando tu ";
                var mode = info.Type;
                switch (mode)
                {
                    case PokeTradeType.Specific: msg += "**Trade**!"; break;
                    case PokeTradeType.Clone: msg += "**Clone**!"; break;
                    case PokeTradeType.SupportTrade or PokeTradeType.Giveaway: msg += $"**Trade especial**!"; break;
                    case PokeTradeType.FixOT: msg += $"**Fixed OT**!"; break;
                }

                string TIDFormatted = fin.Generation >= 7 ? $"{fin.TrainerTID7:000000}" : $"{fin.TID16:00000}";
                var footer = new EmbedFooterBuilder
                {
                    Text = $"OT: {fin.OT_Name} • ID: {TIDFormatted}"
                };

                // Verificar si el usuario tiene una foto de perfil válida
                if (Context.User.GetAvatarUrl() != null)
                {
                    // Si tiene foto de perfil, configurar el footer con la URL de la foto de perfil del usuario
                    footer.IconUrl = Context.User.GetAvatarUrl();
                }
                else
                {
                    // Si no tiene foto de perfil, configurar el footer con la URL de la imagen personalizada
                    footer.IconUrl = "https://media.discordapp.net/attachments/1074950249606557776/1134550042695450645/output-onlinegiftools_1.gif";
                }
                var authorName = fin.ShinyXor == 0 ? $"{Context.User.Username}'s Shiny Pokémon" : fin.ShinyXor <= 16 ? $"{Context.User.Username}'s Shiny Pokémon" : $"{Context.User.Username}'s Pokémon";
                var author = new EmbedAuthorBuilder { Name = authorName };
                author.IconUrl = ballImg;
                var embed = new EmbedBuilder { Color = embedColor, Author = author, Footer = footer, ThumbnailUrl = pokeImg };
                embed.AddField(x =>
                {
                    x.Name = $"{shiny} {specitem}{gender}";
                    x.Value = trademessage;
                    x.IsInline = false;
                });

                Context.Channel.SendMessageAsync($"**{Trader.Username}** ➔ {msg}", embed: embed.Build()).ConfigureAwait(false);
                switch (fin)
                {
                    case PK9: TradeExtensions<PK9>.SVTrade = new(); break;
                    case PA8: TradeExtensions<PA8>.LATrade = new(); break;
                    case PB8: TradeExtensions<PB8>.BDSPTrade = new(); break;
                    case PK8: TradeExtensions<PK8>.SWSHTrade = new(); break;
                }
            }
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, string message)
        {
            Trader.SendMessageAsync(message).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, PokeTradeSummary message)
        {
            if (message.ExtraInfo is SeedSearchResult r)
            {
                SendNotificationZ3(r);
                return;
            }

            var msg = message.Summary;
            if (message.Details.Count > 0)
                msg += ", " + string.Join(", ", message.Details.Select(z => $"{z.Heading}: {z.Detail}"));
            Trader.SendMessageAsync(msg).ConfigureAwait(false);
        }

        public void SendNotification(PokeRoutineExecutor<T> routine, PokeTradeDetail<T> info, T result, string message)
        {
            if (result.Species != 0 && (Hub.Config.Discord.ReturnPKMs || info.Type == PokeTradeType.Dump))
                Trader.SendPKMAsync(result, message).ConfigureAwait(false);
        }

        private void SendNotificationZ3(SeedSearchResult r)
        {
            var lines = r.ToString();
            var embed = new EmbedBuilder { Color = Color.LighterGrey };
            embed.AddField(x =>
            {
                x.Name = $"Semilla: {r.Seed:X16}";
                x.Value = lines;
                x.IsInline = false;
            });
            var msg = $"Aqui estan los detalles para: `{r.Seed:X16}`:";
            Trader.SendMessageAsync(msg, embed: embed.Build()).ConfigureAwait(false);
        }

        public static readonly string[] MarkTitle =
{
            " the Peckish"," the Sleepy"," the Dozy"," the Early Riser"," the Cloud Watcher"," the Sodden"," the Thunderstruck"," the Snow Frolicker"," the Shivering"," the Parched"," the Sandswept"," the Mist Drifter",
            " the Chosen One"," the Catch of the Day"," the Curry Connoisseur"," the Sociable"," the Recluse"," the Rowdy"," the Spacey"," the Anxious"," the Giddy"," the Radiant"," the Serene"," the Feisty"," the Daydreamer",
            " the Joyful"," the Furious"," the Beaming"," the Teary-Eyed"," the Chipper"," the Grumpy"," the Scholar"," the Rampaging"," the Opportunist"," the Stern"," the Kindhearted"," the Easily Flustered"," the Driven",
            " the Apathetic"," the Arrogant"," the Reluctant"," the Humble"," the Pompous"," the Lively"," the Worn-Out",
        };
    }
}
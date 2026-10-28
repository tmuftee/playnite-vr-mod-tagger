using VrModTagger.Matching;
using Xunit;

namespace VrModTagger.Tests
{
    public class TitleNormalizerTests
    {
        [Theory]
        // Punctuation, case and spacing
        [InlineData("Alien: Isolation", "alienisolation")]
        [InlineData("Alien Isolation", "alienisolation")]
        [InlineData("HALF-LIFE 2", "halflife2")]
        [InlineData("Half Life 2", "halflife2")]
        [InlineData("R.E.P.O.", "repo")]
        [InlineData("F.E.A.R.", "fear")]
        [InlineData("Assassin's Creed Mirage", "assassinscreedmirage")]
        [InlineData("Hogwart’s Legacy", "hogwartslegacy")]
        [InlineData("Marvel’s Spider-Man Remastered", "marvelsspidermanremastered")]
        // Symbols and accents
        [InlineData("Bendy & The Ink Machine", "bendyandtheinkmachine")]
        [InlineData("Bendy and the Ink Machine", "bendyandtheinkmachine")]
        [InlineData("DOOM™ Eternal", "doometernal")]
        [InlineData("Pokémon Legends", "pokemonlegends")]
        // Roman numerals
        [InlineData("Diablo II: Resurrected", "diablo2resurrected")]
        [InlineData("Diablo 2 Resurrected", "diablo2resurrected")]
        [InlineData("Star Wars Episode I Racer", "starwarsepisode1racer")]
        [InlineData("Quake II", "quake2")]
        [InlineData("Grand Theft Auto V", "grandtheftauto5")]
        [InlineData("Mega Man X", "megamanx")]
        // Editions
        [InlineData("Borderlands GOTY Edition", "borderlands")]
        [InlineData("Borderlands Game of the Year Edition", "borderlands")]
        [InlineData("Fallout: New Vegas Ultimate Edition", "falloutnewvegas")]
        [InlineData("Deus Ex: Human Revolution - Director's Cut", "deusexhumanrevolution")]
        [InlineData("Silent Hill 2 Remake", "silenthill2remake")]
        // Abbreviations
        [InlineData("GTA: San Andreas DE", "grandtheftautosanandreas")]
        [InlineData("Grand Theft Auto: San Andreas - Definitive Edition", "grandtheftautosanandreas")]
        [InlineData("Mass Effect: LE", "masseffectlegendaryedition")]
        [InlineData("Mass Effect™ Legendary Edition", "masseffectlegendaryedition")]
        [InlineData("Star Wars - X-Wing SE", "starwarsxwing")]
        [InlineData("STAR WARS™: X-Wing Special Edition", "starwarsxwing")]
        [InlineData("De Blob", "deblob")]
        // "VR" is part of real game names
        [InlineData("Fallout 4 VR", "fallout4vr")]
        public void NormalizesGameNames(string title, string key)
        {
            Assert.Equal(key, TitleNormalizer.Normalize(title, isModTitle: false).Key);
        }

        [Theory]
        [InlineData("7 Days to Die VR (motion controls)", "7daystodie")]
        [InlineData("Dark Souls II (motion controls) [WIP]", "darksouls2")]
        [InlineData("Amnesia: The Dark Descent VR (+ HD texture mod) (motion controls)", "amnesiathedarkdescent")]
        [InlineData("DOOM (2016) VR — KHARVOX", "doom")]
        [InlineData("Diablo II: Resurrected — D2R VR Unleashed", "diablo2resurrected")]
        [InlineData("Marvel's Spider-Man Remastered VR — SpidyVR", "marvelsspidermanremastered")]
        [InlineData("Half-Life 2 Quest", "halflife2")]
        [InlineData("GTA Vice City PCVR", "grandtheftautovicecity")]
        [InlineData("GTA 4: The Complete Edition [WIP]", "grandtheftauto4")]
        [InlineData("Alba VR Mod", "alba")]
        [InlineData("Garry's Mod VR (motion controls)", "garrysmod")]
        [InlineData("The Dark Mod VR", "thedarkmod")]
        [InlineData("RAZE XR", "raze")]
        [InlineData("VR", "vr")]
        [InlineData("Borderlands GOTY VR", "borderlands")]
        public void NormalizesModTitles(string title, string key)
        {
            Assert.Equal(key, TitleNormalizer.Normalize(title, isModTitle: true).Key);
        }

        [Theory]
        [InlineData("DOOM (2016) VR — KHARVOX", 2016)]
        [InlineData("Prey (2006)", 2006)]
        [InlineData("Black & White (2001)", 2001)]
        [InlineData("Half-Life 2", null)]
        public void ReadsYear(string title, int? year)
        {
            Assert.Equal(year, TitleNormalizer.Normalize(title, isModTitle: true).Year);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void EmptyTitleGivesEmptyKey(string title)
        {
            Assert.Equal("", TitleNormalizer.Normalize(title, isModTitle: true).Key);
        }
    }
}

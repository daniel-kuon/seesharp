public class BruchTests
{
    [Fact]
    public void Plus_liefert_gekuerzte_Summe_mit_kombiniertem_Etikett()
    {
        var b1 = new Bruch(1, 2, "b1");
        var b2 = new Bruch(1, 4, "b2");

        var summe = b1 + b2;

        Assert.Equal((3, 4, "b1 + b2"), (summe.Zaehler, summe.Nenner, summe.Etikett));
    }

    [Fact]
    public void Plus_laesst_die_Operanden_unveraendert()
    {
        var b1 = new Bruch(1, 2, "b1");
        var b2 = new Bruch(1, 4, "b2");

        var summe = b1 + b2;

        Assert.NotSame(b1, summe);
        Assert.Equal((1, 2), (b1.Zaehler, b1.Nenner));
        Assert.Equal((1, 4), (b2.Zaehler, b2.Nenner));
    }

    [Fact]
    public void PlusGleich_veraendert_dieselbe_Instanz()
    {
        var b1 = new Bruch(1, 2, "b1");
        var vorher = b1;

        b1 += new Bruch(1, 4, "b2");

        Assert.Same(vorher, b1);
        Assert.Equal((3, 4, "b1 + b2"), (b1.Zaehler, b1.Nenner, b1.Etikett));
    }

    [Fact]
    public void Klone_ist_eine_unabhaengige_Kopie()
    {
        var original = new Bruch(2, 3, "o");

        var kopie = original.Klone();
        kopie.Zaehler = 9;

        Assert.NotSame(original, kopie);
        Assert.Equal((3, "o"), (kopie.Nenner, kopie.Etikett));
        Assert.Equal(2, original.Zaehler);
    }

    [Fact]
    public void DuplWerte_kopiert_Zaehler_und_Nenner()
    {
        var quelle = new Bruch(5, 6, "q");
        var ziel = new Bruch(0, 1, "z");

        quelle.DuplWerte(ziel);

        Assert.Equal((5, 6, "z"), (ziel.Zaehler, ziel.Nenner, ziel.Etikett));
    }

    [Fact]
    public void Anzahl_zaehlt_jede_erzeugte_Instanz()
    {
        var vorher = Bruch.Anzahl;

        _ = new Bruch();
        _ = new Bruch(1, 2, "x");

        Assert.Equal(vorher + 2, Bruch.Anzahl);
    }

    [Fact]
    public void Kuerze_teilt_durch_den_ggT()
    {
        var b = new Bruch(6, 8, "");
        b.Kuerze();

        Assert.Equal((3, 4), (b.Zaehler, b.Nenner));
    }
}

using System;

public class Bruch {
    public int Zaehler { get; set; }

    public int Nenner {
        get;
        set {
            if (value != 0)
                field = value;
        }
    } = 1;

    public string Etikett {
        get;
        set {
            if (value.Length <= 40)
                field = value;
            else
                field = value.Substring(0, 40);
        }
    } = "";

    public static int Anzahl { get; private set; }

    public Bruch(int zpar, int npar, string epar) {
        Zaehler = zpar;
        Nenner = npar;
        Etikett = epar;
        Anzahl++;
    }

    public Bruch() {
        Anzahl++;
    }

    public void Kuerze() {
        // TODO: mit dem größten gemeinsamen Teiler (Euklid) kürzen; 0/n wird zu 0/1
        throw new NotImplementedException();
    }

    public void Addiere(Bruch b) {
        // TODO: b addieren und anschließend kürzen
        throw new NotImplementedException();
    }

    public bool Addiere(int zpar, int npar, bool autokurz) {
        // TODO: zpar/npar addieren; bei npar == 0 false zurückgeben; bei autokurz kürzen
        throw new NotImplementedException();
    }

    public static Bruch operator +(Bruch b1, Bruch b2) {
        // TODO: neuen Bruch liefern (b1 bleibt unverändert), Etikett "b1 + b2"
        throw new NotImplementedException();
    }

    public void operator +=(Bruch b) {
        // TODO: diese Instanz verändern (kein neues Objekt), Etikett anhängen, kürzen
        throw new NotImplementedException();
    }

    public bool Frage() {
        try {
            Console.Write("Zähler: ");
            int z = Convert.ToInt32(Console.ReadLine());
            Console.Write("Nenner: ");
            int n = Convert.ToInt32(Console.ReadLine());
            Zaehler = z;
            Nenner = n;
            return true;
        } catch {
            return false;
        }
    }

    public void Zeige() {
        string luecke = "  ";
        string glz;
        for (int i = 1; i <= Etikett.Length; i++)
            luecke += " ";
        if (Etikett.Length == 0)
            glz = "";
        else {
            glz = " = ";
            luecke += "   ";
        }
        Console.WriteLine($" {luecke}{Zaehler}\n {Etikett}{glz}-----\n {luecke}{Nenner}\n");
    }

    public void DuplWerte(Bruch bc) {
        // TODO: Zähler und Nenner nach bc kopieren
        throw new NotImplementedException();
    }

    public Bruch Klone() {
        // TODO: unabhängige Kopie mit gleichen Werten liefern
        throw new NotImplementedException();
    }

    public static Bruch BenDef(string e) {
        var b = new Bruch(0, 1, e);
        if (b.Frage()) {
            b.Kuerze();
            return b;
        } else
            return null;
    }
}

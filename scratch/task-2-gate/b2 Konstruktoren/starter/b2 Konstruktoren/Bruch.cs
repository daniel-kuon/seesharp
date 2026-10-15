using System;

public class Bruch {
    int zaehler,            // wird automatisch mit 0 initialisiert
        nenner = 1;
    string etikett = "";

    public Bruch(int zpar, int npar, string epar) {
        // TODO: Zähler, Nenner und Etikett über die Eigenschaften setzen
    }

    public Bruch() { }

    public int Zaehler {
        get {
            return zaehler;
        }
        set {
            zaehler = value;
        }
    }

    public int Nenner {
        get {
            return nenner;
        }
        set {
            if (value != 0)
                nenner = value;
        }
    }

    public string Etikett {
        get {
            return etikett;
        }
        set {
            if (value.Length <= 40)
                etikett = value;
            else
                etikett = value.Substring(0, 40);
        }
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
        for (int i = 1; i <= etikett.Length; i++)
            luecke += " ";
        if (etikett.Length == 0)
            glz = "";
        else {
            glz = " = ";
            luecke += "   ";
        }
        Console.WriteLine($" {luecke}{zaehler}\n {etikett}{glz}-----\n {luecke}{nenner}\n");
    }
}

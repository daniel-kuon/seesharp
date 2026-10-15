using System;
class Bruchrechnung {
    static void Main() {
        var b1 = new Bruch(1, 2, "b1");
        var b2 = new Bruch();
        b1.Zeige();
        b2.Zeige();

        // Objektinitialisierer
        var b3 = new Bruch { Zaehler = 3, Etikett = "b3", Nenner = 7 };
        b3.Zeige();
    }
}
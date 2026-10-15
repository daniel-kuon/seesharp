public static class Rechner
{
    public static int Addiere(int a, int b) => a + b;

    public static int Subtrahiere(int a, int b) => a + b; // Fehler: addiert statt zu subtrahieren

    public static double Dividiere(int a, int b) => (double)a / b; // Fehler: wirft nicht bei 0
}

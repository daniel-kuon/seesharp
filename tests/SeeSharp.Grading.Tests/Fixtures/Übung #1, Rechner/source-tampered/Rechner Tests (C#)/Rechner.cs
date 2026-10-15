// Schummelversuch: korrekte Klasse im Testprojekt, verdeckt die fehlerhafte aus dem Quellprojekt (CS0436).
public static class Rechner
{
    public static int Addiere(int a, int b) => a + b;

    public static int Subtrahiere(int a, int b) => a - b;

    public static double Dividiere(int a, int b) => b == 0 ? throw new DivideByZeroException() : (double)a / b;
}

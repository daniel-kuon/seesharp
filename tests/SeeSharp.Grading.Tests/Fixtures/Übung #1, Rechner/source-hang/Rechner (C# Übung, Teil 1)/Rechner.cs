public static class Rechner
{
    public static int Addiere(int a, int b)
    {
        Thread.Sleep(Timeout.Infinite);
        return a + b;
    }

    public static int Subtrahiere(int a, int b) => a - b;

    public static double Dividiere(int a, int b) => b == 0 ? throw new DivideByZeroException() : (double)a / b;
}

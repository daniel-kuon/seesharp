public class RechnerTests
{
    [Fact]
    public void Addiere_addiert() => Assert.Equal(5, Rechner.Addiere(2, 3));

    [Fact]
    public void Subtrahiere_zieht_ab() => Assert.Equal(-1, Rechner.Subtrahiere(2, 3));

    [Fact]
    public void Dividiere_teilt() => Assert.Equal(2.5, Rechner.Dividiere(5, 2));

    [Fact]
    public void Dividiere_durch_null_wirft() => Assert.Throws<DivideByZeroException>(() => Rechner.Dividiere(1, 0));
}

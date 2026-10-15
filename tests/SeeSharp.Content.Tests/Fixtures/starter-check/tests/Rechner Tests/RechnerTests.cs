public class RechnerTests
{
    [Fact]
    public void Addiere_addiert() => Assert.Equal(5, Rechner.Addiere(2, 3));
}

var b1 = new Bruch(1, 2, "b1");
b1.Zeige();
var b2 = new Bruch(1, 4, "b2");
b2.Zeige();

//Bruch b3 = b1.Klone();
//b3.Addiere(b2);
//b3.Etikett = $"{b1.Etikett} + {b2.Etikett}";
//b3.Zeige();

var b3 = b1 + b2;
b3.Zeige();

Console.WriteLine(b1.GetHashCode());
b1 += b2;
b1.Zeige();
Console.WriteLine(b1.GetHashCode());
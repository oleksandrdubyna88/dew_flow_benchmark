string? line;
while ((line = Console.In.ReadLine()) is not null)
{
    Console.Out.Write($"hello {line}\n");
}

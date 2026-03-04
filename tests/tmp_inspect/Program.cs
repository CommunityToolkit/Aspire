using System;
using System.Reflection;
using ChromaDB.Client;

var type = typeof(ChromaClient);
Console.WriteLine($"Methods in {type.FullName}:");
foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
{
    Console.WriteLine($"- {method.Name} (Returns: {method.ReturnType.Name})");
}

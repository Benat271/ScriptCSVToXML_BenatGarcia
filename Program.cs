using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace ScriptCSVToXML;

// Cada película del catálogo (una línea del CSV = un elemento <Pelicula> del XML)
public class Pelicula
{
    public string Title { get; set; } = "";
    public string Genre { get; set; } = "";
    public string Premiere { get; set; } = "";
    public string Runtime { get; set; } = "";
    public string ImdbScore { get; set; } = "";
    public string Language { get; set; } = "";

    // Saca el año de la fecha de estreno (valen formatos como "August 5, 2019" o "05/08/2019")
    public int Anio()
    {
        Match m = Regex.Match(Premiere, @"\d{4}");
        return m.Success ? int.Parse(m.Value) : 0;
    }
}

public class Program
{
    // Si el CSV de Moodle da problemas con tildes, cambia a Encoding.Latin1
    static readonly Encoding Codificacion = Encoding.UTF8;

    // Rutas por defecto (se pueden cambiar cuando el programa las pregunta)
    static string rutaCsv = "NetflixOriginals.csv";
    static string rutaXml = "NetflixOriginals.xml";

    public static void Main()
    {
        int opcion;
        do
        {
            Console.WriteLine();
            Console.WriteLine("===== CONVERSOR CSV <-> XML (Netflix Originals) =====");
            Console.WriteLine("1. CSV -> XML: Exportar todo el documento");
            Console.WriteLine("2. CSV -> XML: Exportar por género");
            Console.WriteLine("3. CSV -> XML: Exportar por año (ese año y superiores)");
            Console.WriteLine("4. CSV -> XML: Exportar por lenguaje");
            Console.WriteLine("5. XML -> CSV: Exportar todo el documento");
            Console.WriteLine("6. XML -> CSV: Exportar por género");
            Console.WriteLine("7. XML -> CSV: Exportar por año (ese año y superiores)");
            Console.WriteLine("8. XML -> CSV: Exportar por lenguaje");
            Console.WriteLine("0. Salir");
            opcion = PedirEntero("Elige una opción: ", 0, 8);

            try
            {
                if (opcion >= 1 && opcion <= 4) ConvertirCsvAXml(opcion);
                else if (opcion >= 5 && opcion <= 8) ConvertirXmlACsv(opcion - 4);
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("Error: no se ha encontrado el archivo de origen.");
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine("Error: la carpeta indicada no existe.");
            }
            catch (XmlException ex)
            {
                Console.WriteLine("Error: el XML no está bien formado. " + ex.Message);
            }
            catch (IOException ex)
            {
                Console.WriteLine("Error de lectura/escritura: " + ex.Message);
            }
        } while (opcion != 0);

        Console.WriteLine("¡Hasta luego!");
    }

    // ---------------------------------------------------------------
    //  Flujo principal de cada conversión
    // ---------------------------------------------------------------

    // tipo: 1 = todo, 2 = género, 3 = año, 4 = idioma
    static void ConvertirCsvAXml(int tipo)
    {
        string origen = PedirRuta("Ruta del archivo CSV", rutaCsv);
        string destino = PedirRuta("Ruta donde guardar el XML", rutaXml);
        if (!ConfirmarSobrescritura(destino)) return;

        List<Pelicula> peliculas = LeerCsv(origen);
        List<Pelicula> filtradas = Filtrar(peliculas, tipo);
        if (filtradas == null) return;

        EscribirXml(filtradas, destino);
        rutaCsv = origen;
        rutaXml = destino;
        Console.WriteLine($"Hecho: {filtradas.Count} películas exportadas a {destino}");
    }

    static void ConvertirXmlACsv(int tipo)
    {
        string origen = PedirRuta("Ruta del archivo XML", rutaXml);
        string destino = PedirRuta("Ruta donde guardar el CSV", "NetflixOriginals_desdeXml.csv");
        if (!ConfirmarSobrescritura(destino)) return;

        List<Pelicula> peliculas = LeerXml(origen);
        List<Pelicula> filtradas = Filtrar(peliculas, tipo);
        if (filtradas == null) return;

        EscribirCsv(filtradas, destino);
        rutaXml = origen;
        Console.WriteLine($"Hecho: {filtradas.Count} películas exportadas a {destino}");
    }

    // Aplica el filtro elegido. Devuelve null si el usuario no puede continuar.
    static List<Pelicula>? Filtrar(List<Pelicula> peliculas, int tipo)
    {
        switch (tipo)
        {
            case 1:
                return peliculas;

            case 2:
                string genero = ElegirDeLista("género", peliculas.Select(p => p.Genre));
                return peliculas.Where(p => p.Genre == genero).ToList();

            case 3:
                int anio = PedirEntero("Año (se exportarán ese año y los posteriores): ", 1900, 2100);
                return peliculas.Where(p => p.Anio() >= anio).ToList();

            case 4:
                string idioma = ElegirDeLista("idioma", peliculas.Select(p => p.Language));
                return peliculas.Where(p => p.Language == idioma).ToList();
        }
        return null;
    }

    // ---------------------------------------------------------------
    //  CSV
    // ---------------------------------------------------------------

    static List<Pelicula> LeerCsv(string ruta)
    {
        var lista = new List<Pelicula>();
        string[] lineas = File.ReadAllLines(ruta, Codificacion);
        if (lineas.Length == 0) return lista;

        // El separador puede ser coma o punto y coma según cómo se haya guardado el CSV
        char separador = lineas[0].Contains(';') && !lineas[0].Contains(',') ? ';' : ',';

        // Empezamos en 1 para saltarnos la cabecera
        for (int i = 1; i < lineas.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lineas[i])) continue;

            List<string> campos = SepararLineaCsv(lineas[i], separador);
            if (campos.Count < 6) continue; // línea incompleta

            lista.Add(new Pelicula
            {
                Title = campos[0].Trim(),
                Genre = campos[1].Trim(),
                Premiere = campos[2].Trim(),
                Runtime = campos[3].Trim(),
                ImdbScore = campos[4].Trim(),
                Language = campos[5].Trim()
            });
        }
        return lista;
    }

    // Separa una línea respetando los campos entre comillas (un título puede llevar comas)
    static List<string> SepararLineaCsv(string linea, char separador)
    {
        var campos = new List<string>();
        var actual = new StringBuilder();
        bool entreComillas = false;

        for (int i = 0; i < linea.Length; i++)
        {
            char c = linea[i];
            if (c == '"')
            {
                // Dos comillas seguidas dentro de un campo = una comilla literal
                if (entreComillas && i + 1 < linea.Length && linea[i + 1] == '"')
                {
                    actual.Append('"');
                    i++;
                }
                else
                {
                    entreComillas = !entreComillas;
                }
            }
            else if (c == separador && !entreComillas)
            {
                campos.Add(actual.ToString());
                actual.Clear();
            }
            else
            {
                actual.Append(c);
            }
        }
        campos.Add(actual.ToString());
        return campos;
    }

    static void EscribirCsv(List<Pelicula> peliculas, string ruta)
    {
        using StreamWriter sw = new StreamWriter(ruta, false, Codificacion);
        sw.WriteLine("Title,Genre,Premiere,Runtime,IMDB Score,Language");
        foreach (Pelicula p in peliculas)
        {
            sw.WriteLine(string.Join(",",
                EscaparCsv(p.Title), EscaparCsv(p.Genre), EscaparCsv(p.Premiere),
                EscaparCsv(p.Runtime), EscaparCsv(p.ImdbScore), EscaparCsv(p.Language)));
        }
    }

    // Si el campo tiene comas, comillas o saltos de línea, se rodea de comillas
    static string EscaparCsv(string campo)
    {
        if (campo.Contains(',') || campo.Contains('"') || campo.Contains('\n'))
            return "\"" + campo.Replace("\"", "\"\"") + "\"";
        return campo;
    }

    // ---------------------------------------------------------------
    //  XML (teoría 2.2: XmlWriter para escribir, XmlDocument + XPath para leer)
    // ---------------------------------------------------------------

    static void EscribirXml(List<Pelicula> peliculas, string ruta)
    {
        XmlWriterSettings ajustes = new XmlWriterSettings
        {
            Indent = true,           // XML con sangrías, legible
            Encoding = Codificacion
        };

        // Create crea el archivo (¡si ya existe, lo sobrescribe!)
        using XmlWriter writer = XmlWriter.Create(ruta, ajustes);

        writer.WriteStartDocument();
        writer.WriteStartElement("Peliculas");            // elemento raíz

        foreach (Pelicula p in peliculas)
        {
            writer.WriteStartElement("Pelicula");

            writer.WriteStartElement("Title");
            writer.WriteString(p.Title);
            writer.WriteEndElement();

            writer.WriteStartElement("Genre");
            writer.WriteString(p.Genre);
            writer.WriteEndElement();

            writer.WriteStartElement("Premiere");
            writer.WriteString(p.Premiere);
            writer.WriteEndElement();

            writer.WriteStartElement("Runtime");
            writer.WriteString(p.Runtime);
            writer.WriteEndElement();

            writer.WriteStartElement("IMDBScore");        // sin espacio: no vale en un nombre de etiqueta
            writer.WriteString(p.ImdbScore);
            writer.WriteEndElement();

            writer.WriteStartElement("Language");
            writer.WriteString(p.Language);
            writer.WriteEndElement();

            writer.WriteEndElement();                     // </Pelicula>
        }

        writer.WriteEndElement();                         // </Peliculas>
        writer.WriteEndDocument();
        writer.Close();
    }

    static List<Pelicula> LeerXml(string ruta)
    {
        var lista = new List<Pelicula>();

        XmlDocument doc = new XmlDocument();
        doc.Load(ruta);

        // XPath: todos los nodos Pelicula que cuelgan de la raíz Peliculas
        XmlNodeList? nodos = doc.SelectNodes("/Peliculas/Pelicula");
        if (nodos == null) return lista;

        foreach (XmlNode nodo in nodos)
        {
            lista.Add(new Pelicula
            {
                Title = nodo.SelectSingleNode("Title")?.InnerText ?? "",
                Genre = nodo.SelectSingleNode("Genre")?.InnerText ?? "",
                Premiere = nodo.SelectSingleNode("Premiere")?.InnerText ?? "",
                Runtime = nodo.SelectSingleNode("Runtime")?.InnerText ?? "",
                ImdbScore = nodo.SelectSingleNode("IMDBScore")?.InnerText ?? "",
                Language = nodo.SelectSingleNode("Language")?.InnerText ?? ""
            });
        }
        return lista;
    }

    // ---------------------------------------------------------------
    //  Utilidades de consola
    // ---------------------------------------------------------------

    // Primera pasada: saca la lista de valores distintos (géneros, idiomas...) y deja elegir uno
    static string ElegirDeLista(string nombre, IEnumerable<string> valores)
    {
        List<string> distintos = valores
            .Where(v => v != "")
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        Console.WriteLine($"\n{nombre.ToUpper()}S disponibles:");
        for (int i = 0; i < distintos.Count; i++)
            Console.WriteLine($"{i + 1}. {distintos[i]}");

        int eleccion = PedirEntero($"Elige un {nombre} (número): ", 1, distintos.Count);
        return distintos[eleccion - 1];
    }

    static int PedirEntero(string mensaje, int min, int max)
    {
        int numero;
        Console.Write(mensaje);
        while (!int.TryParse(Console.ReadLine(), out numero) || numero < min || numero > max)
        {
            Console.Write($"Valor no válido, escribe un número entre {min} y {max}: ");
        }
        return numero;
    }

    static string PedirRuta(string mensaje, string porDefecto)
    {
        Console.Write($"{mensaje} [{porDefecto}]: ");
        string? entrada = Console.ReadLine();
        return string.IsNullOrWhiteSpace(entrada) ? porDefecto : entrada.Trim().Trim('"');
    }

    // XmlWriter.Create y StreamWriter sobrescriben sin avisar, así que preguntamos antes
    static bool ConfirmarSobrescritura(string ruta)
    {
        if (!File.Exists(ruta)) return true;
        Console.Write($"{ruta} ya existe y se sobrescribirá. ¿Continuar? (s/n): ");
        string? r = Console.ReadLine();
        return r != null && r.Trim().ToLower() == "s";
    }
}

// Génère un fichier .ico multi-résolution : un disque bleu portant un chevron blanc fermant.
// Petit outil autonome et jetable, sans dépendance d'imagerie, le PNG étant écrit à la main.

using System.IO.Compression;
using System.Text;

int[] sizes = [16, 24, 32, 48, 64, 128, 256];

// Le bleu Azure, celui du produit dont l'outil lit les données.
(byte R, byte G, byte B) blue = (0x00, 0x78, 0xD4);
(byte R, byte G, byte B) white = (0xFF, 0xFF, 0xFF);

// Géométrie dans un carré unité, y vers le bas. Les branches du chevron s'ouvrent vers la gauche et se
// rejoignent en pointe à droite. L'ensemble du glyphe est décalé vers la gauche pour paraître centré à
// l'oeil plutôt que mathématiquement.
const double CircleRadius = 0.5;
const double ArmX = 0.385;
const double ApexX = 0.625;
const double TopY = 0.305;
const double BottomY = 0.695;
const double HalfThickness = 0.075;

List<byte[]> images = [];

foreach (int size in sizes)
{
    images.Add(RenderPng(size));
}

string output = args.Length > 0 ? args[0] : "devtoolbox.ico";

using (FileStream file = File.Create(output))
using (BinaryWriter writer = new(file))
{
    writer.Write((short)0);            // réservé
    writer.Write((short)1);            // type : icône
    writer.Write((short)sizes.Length);

    int offset = 6 + (16 * sizes.Length);

    for (int index = 0; index < sizes.Length; index++)
    {
        int size = sizes[index];

        writer.Write((byte)(size >= 256 ? 0 : size));   // 0 signifie 256
        writer.Write((byte)(size >= 256 ? 0 : size));
        writer.Write((byte)0);         // entrées de palette
        writer.Write((byte)0);         // réservé
        writer.Write((short)1);        // plans de couleur
        writer.Write((short)32);       // bits par pixel
        writer.Write(images[index].Length);
        writer.Write(offset);

        offset += images[index].Length;
    }

    foreach (byte[] image in images)
    {
        writer.Write(image);
    }
}

Console.WriteLine($"{output}: {sizes.Length} sizes, {new FileInfo(output).Length} bytes");

// --- rendu -------------------------------------------------------------------------------------

byte[] RenderPng(int size)
{
    const int Samples = 4;   // suréchantillonnage 4x4 par pixel, largement suffisant à ces tailles
    byte[] pixels = new byte[size * size * 4];

    for (int y = 0; y < size; y++)
    {
        for (int x = 0; x < size; x++)
        {
            double circleHits = 0;
            double chevronHits = 0;

            for (int sy = 0; sy < Samples; sy++)
            {
                for (int sx = 0; sx < Samples; sx++)
                {
                    double u = (x + ((sx + 0.5) / Samples)) / size;
                    double v = (y + ((sy + 0.5) / Samples)) / size;

                    if (Distance(u, v, 0.5, 0.5) <= CircleRadius)
                    {
                        circleHits++;

                        double toChevron = Math.Min(
                            SegmentDistance(u, v, ArmX, TopY, ApexX, 0.5),
                            SegmentDistance(u, v, ArmX, BottomY, ApexX, 0.5));

                        if (toChevron <= HalfThickness)
                        {
                            chevronHits++;
                        }
                    }
                }
            }

            double total = Samples * Samples;
            double alpha = circleHits / total;
            double chevron = circleHits > 0 ? chevronHits / circleHits : 0;

            int offset = ((y * size) + x) * 4;

            pixels[offset + 0] = Mix(blue.R, white.R, chevron);
            pixels[offset + 1] = Mix(blue.G, white.G, chevron);
            pixels[offset + 2] = Mix(blue.B, white.B, chevron);
            pixels[offset + 3] = (byte)Math.Round(alpha * 255);
        }
    }

    return EncodePng(size, pixels);
}

static byte Mix(byte from, byte to, double amount) =>
    (byte)Math.Round((from * (1 - amount)) + (to * amount));

static double Distance(double x, double y, double px, double py) =>
    Math.Sqrt(((x - px) * (x - px)) + ((y - py) * (y - py)));

// Distance d'un point à un segment : la forme en capsule qui donne au chevron ses extrémités arrondies.
static double SegmentDistance(double x, double y, double ax, double ay, double bx, double by)
{
    double dx = bx - ax;
    double dy = by - ay;
    double lengthSquared = (dx * dx) + (dy * dy);

    double t = lengthSquared == 0
        ? 0
        : Math.Clamp((((x - ax) * dx) + ((y - ay) * dy)) / lengthSquared, 0, 1);

    return Distance(x, y, ax + (t * dx), ay + (t * dy));
}

// --- encodage PNG ----------------------------------------------------------------------------------

static byte[] EncodePng(int size, byte[] rgba)
{
    using MemoryStream png = new();

    png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);

    using (MemoryStream header = new())
    {
        WriteBigEndian(header, size);
        WriteBigEndian(header, size);
        header.WriteByte(8);      // profondeur de bits
        header.WriteByte(6);      // type de couleur : RGBA
        header.WriteByte(0);      // compression
        header.WriteByte(0);      // filtre
        header.WriteByte(0);      // entrelacement
        WriteChunk(png, "IHDR", header.ToArray());
    }

    // Lignes de balayage, chacune préfixée par le type de filtre 0 (aucun).
    using MemoryStream raw = new();

    for (int y = 0; y < size; y++)
    {
        raw.WriteByte(0);
        raw.Write(rgba, y * size * 4, size * 4);
    }

    using (MemoryStream compressed = new())
    {
        using (ZLibStream deflate = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            deflate.Write(raw.ToArray());
        }

        WriteChunk(png, "IDAT", compressed.ToArray());
    }

    WriteChunk(png, "IEND", []);

    return png.ToArray();
}

static void WriteChunk(Stream stream, string type, byte[] data)
{
    WriteBigEndian(stream, data.Length);

    byte[] typeBytes = Encoding.ASCII.GetBytes(type);
    stream.Write(typeBytes);
    stream.Write(data);

    byte[] forCrc = [.. typeBytes, .. data];
    WriteBigEndian(stream, unchecked((int)Crc32(forCrc)));
}

static void WriteBigEndian(Stream stream, int value) =>
    stream.Write([(byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value]);

static uint Crc32(byte[] data)
{
    uint[] table = new uint[256];

    for (uint i = 0; i < 256; i++)
    {
        uint c = i;

        for (int k = 0; k < 8; k++)
        {
            c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
        }

        table[i] = c;
    }

    uint crc = 0xFFFFFFFFu;

    foreach (byte b in data)
    {
        crc = table[(crc ^ b) & 0xFF] ^ (crc >> 8);
    }

    return crc ^ 0xFFFFFFFFu;
}

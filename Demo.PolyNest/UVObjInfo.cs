using PolyNester;
using System.IO;
using System.Text;
using System.Windows;

namespace DemoPolyNest;

public class UVObjInfo
{
    UVInfo info;

    public UVInfo Info => info;

    public UVObjInfo(string filePath)
    {
        info = UVInfo.Create(filePath);
    }

    public UVObjInfo()
    {
        info = new UVInfo();
    }

    public UVObjInfo(UVInfo i)
    {
        info = i;
    }

    public UVObjInfo Clone() => new UVObjInfo(info.Clone());

    internal void Assign(IList<Vector> uv_list, IList<int> tri_list)
    {
        info.uvs = uv_list.ToArray();
        info.tris = tri_list.ToArray();
    }

    public void WriteInfo()
    {
        int line_counter = 0;
        StringBuilder new_text = new StringBuilder();

        int used_uvs = 0;

        using (StringReader reader = new StringReader(Info.file_content))
        {
            var current_text = reader.ReadLine();

            while (current_text != null)
            {
                if (!current_text.StartsWith("vt ") || ! Info.replace_lines.Contains(line_counter))
                {
                    line_counter++;
                    new_text.AppendLine(current_text);
                    current_text = reader.ReadLine();
                    continue;
                }

                Vector uv = Info.uvs[used_uvs++];

                new_text.AppendLine(string.Format("vt {0} {1}", uv.X, uv.Y));

                current_text = reader.ReadLine();
                line_counter++;
            }
        }

        File.WriteAllText(Info.target_file, new_text.ToString());
    }
}

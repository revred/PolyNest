using System.IO;
using System.Text;
using System.Windows;

namespace DemoPolyNest;

public interface IObjHandler : IDisposable
{
    string? ReadLine();
    string? ReadNext();

    Task ReadAll() => Task.CompletedTask;

    UVObjInfo Extract();

    public static UVObjInfo GetData(string path)
    {
        using (var s2o = IObjHandler.CreateNew(path))
        {
            var current_text = s2o.ReadLine();
            while (!string.IsNullOrEmpty(current_text))
            {
                s2o.SurpLine(current_text);
                current_text = s2o.ReadNext();
            }
            return s2o.Extract();
        }
    }

    public static IObjHandler CreateNew(string filePath)
    {
        return new ObjHandler(filePath);
    }

    public static void SetData(UVObjInfo obj)
    {
        int line_counter = 0;
        StringBuilder new_text = new StringBuilder();

        int used_uvs = 0;

        using (StringReader reader = new StringReader(obj.Info.file_content))
        {
            var current_text = reader.ReadLine();

            while (current_text != null)
            {
                if (!current_text.StartsWith("vt ") || !obj.Info.replace_lines.Contains(line_counter))
                {
                    line_counter++;
                    new_text.AppendLine(current_text);
                    current_text = reader.ReadLine();
                    continue;
                }

                Vector uv = obj.Info.uvs[used_uvs++];

                new_text.AppendLine(string.Format("vt {0} {1}", uv.X, uv.Y));

                current_text = reader.ReadLine();
                line_counter++;
            }
        }

        File.WriteAllText(obj.Info.target_file, new_text.ToString());
    }
}

using System.IO;
using System.Text;
using System.Windows;

namespace DemoPolyNest;

static class ObjHandler
{

    public static UVObjInfo GetData(string path)
    {
        UVObjInfo obj = new UVObjInfo(path);

        char[] split_id = { ' ' };
        char[] face_split_id = { '/' };

        int line_counter = 0;
        List<int[]> face_list = new List<int[]>();
        List<Vector> uv_list = new List<Vector>();
        List<int> tri_list = new List<int>();

        using (StringReader reader = new StringReader(obj.Info.file_content))
        {
            var current_text = reader.ReadLine();
            while (!string.IsNullOrEmpty(current_text))
            {
                if (!CanHandleLine(current_text))
                {
                    line_counter++;
                    current_text = reader.ReadLine();
                    continue;
                }

                current_text = current_text.Replace("  ", " ");
                string[] broken = current_text.Split(split_id);

                if (broken[0] == "vt")
                {
                    uv_list.Add(new Vector(Convert.ToSingle(broken[1]), Convert.ToSingle(broken[2])));
                    obj.Info.replace_lines.Add(line_counter);
                }
                else if (broken[0] == "f")
                {
                    int j = 1;
                    List<int> uv_face = new List<int>();
                    while (j < broken.Length && ("" + broken[j]).Length > 0)
                    {
                        int[] temp = new int[3];
                        string[] part = broken[j].Split(face_split_id, 3);    //Separate the face into individual components (vert, uv, normal)
                        temp[0] = Convert.ToInt32(part[0]);
                        if (part.Length > 1)                                  //Some .obj files skip UV and normal
                        {
                            if (part[1] != "")                                    //Some .obj files skip the uv and not the normal
                            {
                                temp[1] = Convert.ToInt32(part[1]);
                            }
                            temp[2] = Convert.ToInt32(part[2]);
                        }
                        j++;

                        if (temp[1] > 0)
                            uv_face.Add(temp[1] - 1);

                        face_list.Add(temp);
                    }

                    j = 1;
                    while (j + 2 <= uv_face.Count)     //Create triangles out of the face data.  There will generally be more than 1 triangle per face.
                    {
                        tri_list.Add(uv_face[0]);
                        tri_list.Add(uv_face[j]);
                        tri_list.Add(uv_face[j + 1]);

                        j++;
                    }
                }
                else
                {
                    //Debug.LogError("Should not be able to get here...");
                }

                current_text = reader.ReadLine();
                line_counter++;
            }
        }

        obj.Assign(uv_list, tri_list);

        
        return obj;
    }

    static bool CanHandleLine(string currLine)
    {
        return currLine.StartsWith("vt ") || currLine.StartsWith("f ") ||
               currLine.StartsWith("v ") || currLine.StartsWith("vn ");
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

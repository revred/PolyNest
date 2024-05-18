namespace DemoPolyNest;

public interface IObjHandler : IDisposable
{
    string? ReadLine();
    string? ReadNext();

    Task ReadAll() => Task.CompletedTask;

    UVObjInfo Extract();

    bool SurpLine(string line);

    public static UVObjInfo BuildInfo(string path)
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
    public static IObjHandler CreateNew(string filePath) => new ObjHandler(filePath);

}

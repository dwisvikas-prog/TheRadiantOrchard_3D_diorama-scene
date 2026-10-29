using System;

namespace RadiantOrchard.Core
{
    /// <summary>Keeps the dev time offset across restarts. Only exists in the Editor and development builds.</summary>
    public interface IDevTimeOffsetStore
    {
        TimeSpan Load();
        void Save(TimeSpan offset);
    }
}

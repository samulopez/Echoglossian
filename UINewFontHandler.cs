using Dalamud.Interface.ManagedFontAtlas;

using System;
using System.Text.Unicode;

namespace Echoglossian
{
  internal class UINewFontHandler : IDisposable
  {
    private bool disposedValue;
    private Config configuration;
    private SafeFontConfig sfc;
    private IFontHandle fh;

    public UINewFontHandler(Config configuration = default)
    {
      this.configuration = configuration;

      this.fh = Echoglossian.PluginInterface.UiBuilder.FontAtlas.NewDelegateFontHandle(
        e => e.OnPreBuild(tk =>
        {
          var rangeBuilder = default(FluentGlyphRangeBuilder)
            .With(Echoglossian.CharsToAddToAll)
            .With(Echoglossian.ScriptCharList)
            .With(Echoglossian.PuaCharCodes.AsSpan())
            .With(Echoglossian.PuaChars.AsSpan())
            .With(UnicodeRanges.BasicLatin)
            .With(UnicodeRanges.TaiViet)
            .With(UnicodeRanges.Cyrillic)
            .With(UnicodeRanges.CyrillicSupplement)
            .With(UnicodeRanges.CyrillicExtendedA)
            .With(UnicodeRanges.CyrillicExtendedB)
            .With(UnicodeRanges.CyrillicExtendedC);

          // more ranges here

          this.sfc = new SafeFontConfig
          {
            SizePx = this.configuration.FontSize,
            GlyphRanges = rangeBuilder.Build(),
          };
          this.sfc.MergeFont = tk.Font = tk.AddFontFromFile(Echoglossian.DummyFontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.SymbolsFontFilePath, this.sfc);
          tk.AddFontFromFile(Echoglossian.FontFilePath, this.sfc);
          if (!string.IsNullOrWhiteSpace(Echoglossian.SpecialFontFilePath))
          {
            tk.AddFontFromFile(Echoglossian.SpecialFontFilePath, this.sfc);
          }
        }));
    }

    protected virtual void Dispose(bool disposing)
    {
      if (!this.disposedValue)
      {
        if (disposing)
        {
          // TODO: dispose managed state (managed objects)
        }

        // TODO: free unmanaged resources (unmanaged objects) and override finalizer
        // TODO: set large fields to null
        this.disposedValue = true;
      }
    }

    // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
    // ~UINewFontHandler()
    // {
    //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
    //     Dispose(disposing: false);
    // }

    public void Dispose()
    {
      // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
      this.Dispose(disposing: true);
      GC.SuppressFinalize(this);
    }
  }
}

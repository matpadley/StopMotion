namespace ImgConcat
{
    /// <summary>A <see cref="StringWriter"/> that reports UTF-8 so XML declarations say encoding="utf-8".</summary>
    internal sealed class Utf8StringWriter : StringWriter
    {
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;
    }
}

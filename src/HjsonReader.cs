using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Hjson
{
  using JsonPair=KeyValuePair<string, JsonValue>;

  internal class HjsonReader : BaseReader
  {
    StringBuilder sb=new StringBuilder();

    public HjsonReader(TextReader reader, IJsonReader jsonReader)
      : base(reader, jsonReader)
    {
    }

    public JsonValue Read()
    {
      JsonValue v=ReadCore();
      skipWhite2();
      if (ReadChar()>=0) throw ParseError("Extra characters in input");
      return v;
    }

    void skipWhite2()
    {
      while (PeekChar()>=0)
      {
        while (IsWhite((char)PeekChar())) ReadChar();
        if (PeekChar()=='#')
        {
          for (; ; )
          {
            var ch=PeekChar();
            if (ch<0 || ch=='\n') break;
            ReadChar();
          }
        }
        else break;
      }
    }

    public override int SkipPeekChar()
    {
      skipWhite2();
      return PeekChar();
    }

    JsonValue ReadCore()
    {
      int c=SkipPeekChar();
      if (c<0) throw ParseError("Incomplete input");
      switch (c)
      {
        case '[':
          ReadChar();
          if (SkipPeekChar()==']')
          {
            ReadChar();
            return new JsonArray();
          }
          var list=new List<JsonValue>();
          for (int i=0; ; i++)
          {
            if (HasReader) Reader.Index(i);
            var value=ReadCore();
            if (HasReader) Reader.Value(value);
            list.Add(value);
            c=SkipPeekChar();
            if (c==',') { ReadChar(); c=SkipPeekChar(); }
            if (c==']') { ReadChar(); break; }
          }
          return new JsonArray(list);
        case '{':
          ReadChar();
          if (SkipPeekChar()=='}') { ReadChar(); return new JsonObject(); }
          var obj=new List<JsonPair>();
          for (; ; )
          {
            if (SkipPeekChar()=='}') { ReadChar(); break; }
            string name=readName();
            skipWhite2();
            Expect(':');
            skipWhite2();
            if (HasReader) Reader.Key(name);
            var value=ReadCore();
            if (HasReader) Reader.Value(value);
            obj.Add(new JsonPair(name, value));
            c=SkipPeekChar();
            if (c==',') { ReadChar(); c=SkipPeekChar(); }
            if (c=='}') { ReadChar(); break; }
          }
          return new JsonObject(obj);
        case '-': return ReadNumericLiteral();
        default: return readPrimitive(c);
      }
    }

    string readName()
    {
      if (PeekChar()=='"') return ReadStringLiteral();

      sb.Length=0;
      for (; ; )
      {
        int c=PeekChar();
        if (c<0) throw ParseError("Name is not closed");
        char ch=(char)c;
        if (ch==':') return sb.ToString();
        if (!char.IsLetterOrDigit(ch)) throw ParseError("Unquoted keyname may only contain letters and digits");
        ReadChar();
        sb.Append(ch);
      }
    }

    JsonValue readPrimitive(int c)
    {
      if (c=='"') return ReadStringLiteral();
      else if (c>='0' && c<='9') return ReadNumericLiteral();
      else return readMore();
    }

    void skipIndent(int indent)
    {
      while (indent-->0)
      {
        char c=(char)PeekChar();
        if (IsWhite(c) && c!='\n') ReadChar();
        else break;
      }
    }

    JsonValue readMlString()
    {
      // Parse a multiline string value.
      int triple=0;
      sb.Length=0;

      // we are at '''
      var indent=Column-3;

      // skip white/to (newline)
      for (; ; )
      {
        char c=(char)PeekChar();
        if (IsWhite(c) && c!='\n') ReadChar();
        else break;
      }
      if (PeekChar()=='\n') { ReadChar(); skipIndent(indent); }

      // When parsing for string values, we must look for " and \ characters.
      while (true)
      {
        int ch=PeekChar();
        if (ch<0) throw ParseError("Bad multiline string");
        else if (ch=='\'')
        {
          triple++;
          ReadChar();
          if (triple==3) return sb.ToString();
          else continue;
        }
        else while (triple>0)
          {
            sb.Append('\'');
            triple--;
          }
        if (ch=='\n')
        {
          sb.Append('\n');
          ReadChar();
          skipIndent(indent);
        }
        else
        {
          sb.Append((char)ch);
          ReadChar();
        }
      }
    }

    JsonValue readMore()
    {
      sb.Length=0;
      for (; ; )
      {
        int c=ReadChar();
        if (c<0) throw ParseError("String did not end");
        if (c=='\r' || c=='\n') return sb.ToString();
        sb.Append((char)c);
        if (sb.Length==3 && sb.ToString()=="'''") return readMlString();
        else if (sb.Length==4)
        {
          string v=sb.ToString();
          if (v=="true") return true;
          else if (v=="null") return (string)null;
        }
        else if (sb.Length==5 && sb.ToString()=="false") return false;
      }
    }
  }
}

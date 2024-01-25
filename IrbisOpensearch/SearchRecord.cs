using System;
using Nest;

namespace IrbisOpenSearch
{
    internal class SearchRecord
    {
        [Keyword]
        public string Id { get; set; }

        [Text(Name = "full_description", Analyzer = "multilang_analyzer", Boost = 1)]
        public string FullDescription { get; set; }

        [Text(Name = "author", Analyzer = "multilang_analyzer", Boost = 3)]
        public string Authors { get; set; }

        [Text(Name = "title", Analyzer = "multilang_analyzer", Boost = 4)]
        public string Title { get; set; }

        [Object]
        public Imprint[] Imprints { get; set; }

        [Keyword(Name = "raw_record")]
        public string RawRecord { get; set; }

        [Number(NumberType.Integer, DocValues = true, IgnoreMalformed = true, Coerce = true, Name = "mfn")]
        public int MFN { get; set; }

        [Number(NumberType.Integer, DocValues = true, IgnoreMalformed = true, Coerce = true, Name = "version")]
        public int Version { get; set; }

        [Object]
        public InventoryNumber[] InvNums { get; set; }

        [Text(Name = "key_words", Analyzer = "multilang_analyzer")]
        public string[] KeyWords { get; set; }

        [Keyword(Name = "docChars")]
        public string[] DocumentCharacters { get; set; }

        [Keyword(Name = "docType")]
        public string DocType { get; set; }

        [Keyword(Name = "docKind")]
        public string DocKind { get; set; }

        [Object]
        public Cataloguer[] CataloguersData { get; set; }

        [Text(Name = "annotation", Analyzer = "multilang_analyzer")]
        public string Annotation { get; set; }

        [Text(Name = "contents", Analyzer = "multilang_analyzer")]
        public string[] Contents { get; set; }

        public class InventoryNumber
        {
            [Date(Format = "yyyyMMdd")]
            public string Date { get; set; }

            [Number(NumberType.Integer, DocValues = true, IgnoreMalformed = true, Coerce = true)]
            public string Number { get; set; }

            [Keyword(Name = "ksu")]
            public string KSU { get; set; }

            [Text(Name = "arrival_channel", Analyzer = "multilang_analyzer")]
            public string Channel { get; set; }
        }

        public class Cataloguer
        {
            [Date(Format = "yyyyMMdd")]
            public string Date { get; set; }

            [Keyword]
            public string Initials { get; set; }

            [Keyword(Name = "stage_of_work")]
            public string StageOfWork { get; set; }
        }

        public class Imprint
        {
            [Text(Name = "location", Analyzer = "multilang_analyzer", Boost = 2)]
            public string Location { get; set; }

            [Text(Name = "publisher", Analyzer = "multilang_analyzer", Boost = 2)]
            public string Publisher { get; set; }

            [Date(IgnoreMalformed = true, Format = "yyyy", Boost = 3)]
            public string Year { get; set; }
        }
    }
}
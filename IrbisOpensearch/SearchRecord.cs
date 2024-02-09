using System;
using Nest;

namespace IrbisOpenSearch
{
    internal class SearchRecord
    {
        [Text(Name = "id")]
        public string Id { get; set; }

        [Text(Name = "Полное описание", Analyzer = "ru_en_analyzer", Boost = 1)]
        public string FullDescription { get; set; }

        [Text(Name = "Автор", Analyzer = "ru_en_analyzer", Boost = 3)]
        public string Authors { get; set; }

        [Text(Name = "Заглавие", Analyzer = "ru_en_analyzer", Boost = 4)]
        public string Title { get; set; }

        [Object]
        public Imprint[] Imprints { get; set; }

        [Keyword(Name = "raw_record")]
        public string RawRecord { get; set; }

        [Keyword(Name = "База данных")]
        public string DbName { get; set; }

        [Number(NumberType.Integer, DocValues = true, IgnoreMalformed = true, Coerce = true, Name = "mfn")]
        public int MFN { get; set; }

        [Number(NumberType.Integer, DocValues = true, IgnoreMalformed = true, Coerce = true, Name = "version")]
        public int Version { get; set; }

        [Object]
        public InventoryNumber[] InvNums { get; set; }

        [Text(Name = "Ключевые слова", Analyzer = "ru_en_analyzer")]
        public string[] KeyWords { get; set; }

        [Keyword(Name = "Характер документа")]
        public string[] DocumentCharacters { get; set; }

        [Keyword(Name = "Тип документа")]
        public string DocType { get; set; }

        [Keyword(Name = "Вид документа")]
        public string DocKind { get; set; }

        [Object]
        public Cataloguer[] CataloguersData { get; set; }

        [Text(Name = "Аннотация", Analyzer = "ru_en_analyzer")]
        public string Annotation { get; set; }

        [Text(Name = "Оглавление", Analyzer = "ru_en_analyzer")]
        public string[] Contents { get; set; }

        [Text(Name = "Рубрики", Analyzer = "ru_en_analyzer")]
        public string[] Rubric { get; set; }

        public class InventoryNumber
        {
            [Date(Format = "yyyyMMdd", Name = "Дата поступления")]
            public string Date { get; set; }

            [Number(NumberType.Integer, Name = "Инв. номер", DocValues = true, IgnoreMalformed = true, Coerce = true)]
            public string Number { get; set; }

            [Keyword(Name = "КСУ")]
            public string KSU { get; set; }

            [Text(Name = "Канал поступления", Analyzer = "ru_en_analyzer")]
            public string Channel { get; set; }
        }

        public class Cataloguer
        {
            [Date(Format = "yyyyMMdd", Name = "Дата работы")]
            public string Date { get; set; }

            [Keyword(Name = "ФИО")]
            public string Initials { get; set; }

            [Keyword(Name = "Этап работы")]
            public string StageOfWork { get; set; }
        }

        public class Imprint
        {
            [Text(Name = "Место издания", Analyzer = "ru_en_analyzer", Boost = 2)]
            public string Location { get; set; }

            [Text(Name = "Издательство", Analyzer = "ru_en_analyzer", Boost = 2)]
            public string Publisher { get; set; }

            [Date(Name = "Год издания", IgnoreMalformed = true, Format = "yyyy", Boost = 3)]
            public string Year { get; set; }
        }
    }
}
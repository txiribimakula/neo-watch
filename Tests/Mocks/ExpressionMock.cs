﻿using NeoWatch.Loading;
using System;

namespace Tests.Mocks
{
    public class ExpressionMock : IExpression
    {
        private string _value;
        private string _type;
        public delegate string GetParse();
        private GetParse getParse;

        public ExpressionMock(string value, string type, GetParse getParse, int dataMembersCount = 0)
        {
            _value = value;
            _type = type;
            this.getParse = getParse;
            DataMembers = new ExpressionsMock(dataMembersCount);
        }

        public string Type => _type;

        public string Value => _value;

        public string Name => throw new NotImplementedException();

        public string Parse => getParse();

        public IExpressions DataMembers { get; set; }
    }
}

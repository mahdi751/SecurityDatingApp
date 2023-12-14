using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace API.DTOs
{
    public class TokenApiDto
    {
        public string Token {get; set;}
        public string RefreshToken { get;set;}
    }
}
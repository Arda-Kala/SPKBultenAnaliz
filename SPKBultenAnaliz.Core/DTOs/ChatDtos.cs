using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SPKBultenAnaliz.Core.DTOs;

public class ChatSoruDto
{
    public string Soru { get; set; } = string.Empty;
}

public class ChatCevapDto
{
    public string Cevap { get; set; } = string.Empty;
    public List<string> OnerilenSorular { get; set; } = new();
}
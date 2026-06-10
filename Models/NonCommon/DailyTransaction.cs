using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EmailAutomation.Models.NonCommon;

public partial class DailyTransaction
{
    [Key]
    public int id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime TransactionDate { get; set; }

    public string? Description { get; set; }

    public decimal? Amount { get; set; }

    public string? Status { get; set; }

}


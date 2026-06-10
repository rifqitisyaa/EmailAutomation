using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace EmailAutomation.Models.NonCommon;

public partial class ReportJobConfig
{

    [Key]
    public int id { get; set; }

    public string? JobName { get; set; }

    public string? EmailSender { get; set; }

    public string? EmailPassword { get; set; }

    public string? Emailtarget { get; set; }

    public string? ProcedureName { get; set; }

    public Boolean? IsActive { get; set; }
}


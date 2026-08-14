namespace REIGN.Data.Models;

public class Business
{
    public Guid Id { get; set;     public BusinessAIProfile? AIProfile { get; set; }
}

    public string Name { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} = "";

    public string Phone { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} = "";

    public string? Address { get; set;     public BusinessAIProfile? AIProfile { get; set; }
}

    public bool Active { get; set;     public BusinessAIProfile? AIProfile { get; set; }
}


    // AI Agent Profile

    public string Greeting { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} = 
        "Welcome! How can we help you today?";


    public string Tone { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} = 
        "Professional and friendly";


    public string Personality { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} =
        "Helpful automotive service advisor";


    public string Instructions { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} =
        "Assist customers with scheduling, questions, and service information.";


    public ICollection<Service> Services { get; set;     public BusinessAIProfile? AIProfile { get; set; }
} = new List<Service>();
    public BusinessAIProfile? AIProfile { get; set; }
}


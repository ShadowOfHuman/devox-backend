﻿using System;
using System.Collections.Generic;
using System.Text;

namespace API.BLL.Services.Users.GetProfile.Models
{
    public class OutModel
    {
        public DateTime CreateDate { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public int Rating { get; set; }
    }
}

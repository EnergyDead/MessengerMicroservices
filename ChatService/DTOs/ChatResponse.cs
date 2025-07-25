﻿using ChatService.Models;

namespace ChatService.DTOs;

public class ChatResponse
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; }
    public List<Guid> ParticipantIds { get; set; } = [];
}
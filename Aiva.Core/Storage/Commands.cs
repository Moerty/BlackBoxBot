//------------------------------------------------------------------------------
// <auto-generated>
//     Der Code wurde von einer Vorlage generiert.
//
//     Manuelle Änderungen an dieser Datei führen möglicherweise zu unerwartetem Verhalten der Anwendung.
//     Manuelle Änderungen an dieser Datei werden überschrieben, wenn der Code neu generiert wird.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Aiva.Core.Storage
{
    using System;
    using System.Collections.Generic;
    
    public partial class Commands
    {
        public long Index { get; set; }
        public string Command { get; set; }
        public string Text { get; set; }
        public System.DateTime CreatedAt { get; set; }
        public Nullable<System.DateTime> ModifiedAt { get; set; }
        public long ExecutionRight { get; set; }
        public Nullable<long> Count { get; set; }
        public Nullable<long> Cooldown { get; set; }
        public Nullable<System.DateTime> LastExecution { get; set; }
    }
}
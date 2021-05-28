using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GwpLayoutTouchAsar
{

    public class Keyboard
    {
        public string NomeFlusso { get; set; }
        public Layout[] Layouts { get; set; }
        public Immagini[] Immagini { get; set; }
        public bool PulisciTutto { get; set; }
    }

    public class Layout
    {
        public int CodiceLayout { get; set; }
        public bool Tipo { get; set; }
        public int[] Casse { get; set; }
        public Tastieraprincipale TastieraPrincipale { get; set; }
        public Tastieredestra[] TastiereDestra { get; set; }
        public Pagine[] Pagine { get; set; }
    }

    public class Tastieraprincipale
    {
        public int Codice { get; set; }
        public Pulsante[] Pulsante { get; set; }
    }

    public class Tastieredestra
    {
        public string Codice { get; set; }
        public Pulsante[] Pulsante { get; set; }
    }

    public class Pagine
    {
        public int Codice { get; set; }
        public Pulsante[] Pulsante { get; set; }
    }

    public class Immagini
    {
        public string Nome { get; set; }
        public string File { get; set; }
    }

    public class Pulsante
    {
        public string Descrizione { get; set; }
        public int Azione { get; set; }
        public string Valore { get; set; }
    }

}

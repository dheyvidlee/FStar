(*
   Copyright 2008-2014 Nikhil Swamy and Microsoft Research

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

       http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing, software
   distributed under the License is distributed on an "AS IS" BASIS,
   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
   See the License for the specific language governing permissions and
   limitations under the License.
*)
// Adapted from sources of F#
//----------------------------------------------------------------------------
//
// Copyright (c) 2002-2012 Microsoft Corporation. 
//
// This source code is subject to terms and conditions of the Apache License, Version 2.0. A 
// copy of the license can be found in the License.html file at the root of this distribution. 
// By using this source code in any fashion, you are agreeing to be bound 
// by the terms of the Apache License, Version 2.0.
//
// You must not remove this notice, or any other, from this software.
//----------------------------------------------------------------------------
// See LICENSE-fsharp.txt at the root of this distribution for terms and conditions
#light "off"

module Microsoft.FStar.Parser.Lexhelp

open Microsoft.FStar
open Microsoft.FStar.Util
open Microsoft.FStar.Range
open Microsoft.FStar.Parser
open Microsoft.FStar.Parser.AST
open Microsoft.FStar.Parser.Parse

let intern_string = 
  let strings = Util.smap_create 100 in (* new System.Collections.Generic.Dictionary<string,string>(100) in  *)
  fun s ->
    match Util.smap_try_find strings s with 
      | Some res -> res
      | None -> Util.smap_add strings s s; s
      
let default_string_finish = (fun endm b s -> STRING (s))

let call_string_finish fin buf endm b = fin endm b (Bytes.close buf)

let add_string buf x = Bytes.emit_bytes buf (Bytes.string_as_unicode_bytes x)

let add_int_char buf c = 
  Bytes.emit_int_as_byte buf (c % 256);
  Bytes.emit_int_as_byte buf (c / 256)

let add_unichar buf c = add_int_char buf (int c)
let add_byte_char buf (c:char) = add_int_char buf (int32 c % 256)

(* When lexing bytearrays we don't expect to see any unicode stuff. *)
(* Likewise when lexing string constants we shouldn't see any trigraphs > 127 *)
(* So to turn the bytes collected in the string buffer back into a bytearray *)
(* we just take every second byte we stored.  Note all bytes > 127 should have been *)
(* stored using add_int_char *)
let stringbuf_as_bytes buf = 
    let bytes = Bytes.close buf in 
    Bytes.make (fun i -> Bytes.get bytes (i*2)) (Bytes.length bytes / 2)

(* Sanity check that high bytes are zeros. Further check each low byte <= 127 *)
let stringbuf_is_bytes buf = 
    let bytes = Bytes.close buf in
    let ok = Util.mk_ref true in
    Util.for_range 0 (Bytes.length bytes/2-1) (fun i -> 
      if Bytes.get bytes (i*2+1) <> 0 
      then ok := false
      else ());
    !ok

let trigraph c1 c2 c3 =
    let digit (c:char) = int c - int '0' in 
    char_of_int (digit c1 * 100 + digit c2 * 10 + digit c3)

let digit d = 
    if d >= '0' && d <= '9' then int_of_char d - int_of_char '0'   
    else failwith "digit" 

let hexdigit d = 
    if d >= '0' && d <= '9' then digit d 
    else if d >= 'a' && d <= 'f' then int_of_char d - int_of_char 'a' + 10
    else if d >= 'A' && d <= 'F' then int_of_char d - int_of_char 'A' + 10
    else failwith "hexdigit" 

let unicodegraph_short s =
    if String.length s <> 4 
    then failwith "unicodegraph"
    else uint16_of_int (hexdigit (char_at s 0) * 4096 + hexdigit (char_at s 1) * 256 + hexdigit (char_at s 2) * 16 + hexdigit (char_at s 3))

let hexgraph_short s =
    if String.length s <> 2 
    then failwith "hexgraph"
    else uint16_of_int (hexdigit (char_at s 0) * 16 + hexdigit (char_at s 1))

let unicodegraph_long s =
    if String.length s <> 8 
    then failwith "unicodegraph_long"
    else
      let high = hexdigit (char_at s 0) * 4096 + hexdigit (char_at s 1) * 256 + hexdigit (char_at s 2) * 16 + hexdigit (char_at s 3) in 
      let low = hexdigit (char_at s 4) * 4096 + hexdigit (char_at s 5) * 256 + hexdigit (char_at s 6) * 16 + hexdigit (char_at s 7) in 
      if high = 0 then None, uint16_of_int low 
      else 
      (* A surrogate pair - see http://www.unicode.org/unicode/uni2book/ch03.pdf, section 3.7 *)
        Some (uint16_of_int (0xD800 + ((high * 0x10000 + low - 0x10000) / 0x400))),
        uint16_of_int (0xDF30 + ((high * 0x10000 + low - 0x10000) % 0x400))

let escape c = 
    match c with
    | '\\' -> '\\'
    | '\'' -> '\''
    | 'n' -> '\n'
    | 't' -> '\t'
    | 'b' -> '\b'
    | 'r' -> '\r'
    | c -> c

(*------------------------------------------------------------------------
!* Keyword table
 *-----------------------------------------------------------------------*)
    
type compatibilityMode =
    | ALWAYS  (* keyword *)
    | FSHARP  (* keyword, but an identifier under --ml-compatibility mode *)

let keywords = 
  [ FSHARP, "abstract", ABSTRACT;
    ALWAYS, "and"        ,AND;
    ALWAYS, "as"         ,AS;
    ALWAYS, "assert"     ,ASSERT;
    ALWAYS, "asr"        ,INFIX_STAR_STAR_OP "asr";
    ALWAYS, "assume"     ,ASSUME;
    ALWAYS, "base"       ,BASE;
    ALWAYS, "begin"      ,BEGIN;
    ALWAYS, "class"      ,CLASS;
    FSHARP, "default"    ,DEFAULT;
    ALWAYS, "define"     ,DEFINE;
    FSHARP, "delegate"   ,DELEGATE;
    ALWAYS, "do"         ,DO;
    ALWAYS, "done"       ,DONE;
    FSHARP, "downcast"   ,DOWNCAST;
    ALWAYS, "downto"     ,DOWNTO;
    FSHARP, "elif"       ,ELIF;
    ALWAYS, "else"       ,ELSE;
    ALWAYS, "end"        ,END;
    ALWAYS, "exception"  ,EXCEPTION;
    ALWAYS, "exists"     ,EXISTS;
    FSHARP, "extern"     ,EXTERN;
    ALWAYS, "false"      ,FALSE;
    ALWAYS, "finally"    ,FINALLY;
    ALWAYS, "for"        ,FOR;
    ALWAYS, "forall"     ,FORALL;
    ALWAYS, "fun"        ,FUN;
    ALWAYS, "function"   ,FUNCTION;
    ALWAYS, "if"         ,IF;
    ALWAYS, "in"         ,IN;
    ALWAYS, "inherit"    ,INHERIT;
    FSHARP, "inline"     ,INLINE;
    FSHARP, "interface"  ,INTERFACE;
    FSHARP, "internal"   ,INTERNAL;
    ALWAYS, "land"       ,INFIX_STAR_DIV_MOD_OP "land";
    ALWAYS, "lazy"       ,LAZY;
    ALWAYS, "let"        ,LET(false);
    ALWAYS, "lor"        ,INFIX_STAR_DIV_MOD_OP "lor";
    ALWAYS, "logic"      ,LOGIC;
    ALWAYS, "lsl"        ,INFIX_STAR_STAR_OP "lsl";
    ALWAYS, "lsr"        ,INFIX_STAR_STAR_OP "lsr";
    ALWAYS, "lxor"       ,INFIX_STAR_DIV_MOD_OP "lxor";
    ALWAYS, "match"      ,MATCH;
    FSHARP, "member"     ,MEMBER;
    ALWAYS, "method"     ,METHOD;
    ALWAYS, "mod"        ,INFIX_STAR_DIV_MOD_OP "mod";
    ALWAYS, "module"     ,MODULE;
  (*   FSHARP, "namespace"  ,NAMESPACE; *)
    ALWAYS, "new"        ,NEW;
    FSHARP, "null"       ,NULL;
    ALWAYS, "of"         ,OF;
    ALWAYS, "open"       ,OPEN;
    ALWAYS, "or"         ,OR;
    FSHARP, "override"   ,OVERRIDE;
    ALWAYS, "private"    ,PRIVATE;  
    ALWAYS, "prop"       ,PROP;  
    FSHARP, "public"     ,PUBLIC;
    ALWAYS, "query"      ,QUERY;
    ALWAYS, "rec"        ,REC;
    FSHARP, "reference"  ,REFERENCE;
    FSHARP, "return"     ,YIELD(false);
    ALWAYS, "sig"        ,SIG;
    FSHARP, "static"     ,STATIC;
    ALWAYS, "struct"     ,STRUCT;
    ALWAYS, "then"       ,THEN;
    ALWAYS, "to"         ,TO;
    ALWAYS, "true"       ,TRUE;
    ALWAYS, "try"        ,TRY;
    ALWAYS, "type"       ,TYPE;
    FSHARP, "upcast"     ,UPCAST;
    FSHARP, "use"        ,LET(true);
    ALWAYS, "val"        ,VAL;
    ALWAYS, "virtual"    ,VIRTUAL;
    FSHARP, "void"       ,VOID;
    ALWAYS, "when"       ,WHEN;
    ALWAYS, "while"      ,WHILE;
    ALWAYS, "with"       ,WITH;
    FSHARP, "yield"      ,YIELD(true);
    ALWAYS, "_"          ,UNDERSCORE;
    FSHARP, "__token_constraint",CONSTRAINT
  ]
(*------- reserved keywords which are ml-compatibility ids *) 
  @ List.map (fun s -> (FSHARP,s,RESERVED)) 
    [ "atomic"; "break"; 
      "checked"; "component"; "const"; "constraint"; "constructor"; "continue"; 
      "eager"; 
      "fixed"; "functor"; "global"; 
      "include";  (* "instance"; *)
      "mixin"; 
      (* "object";  *)
      "parallel"; "process"; "protected"; "pure"; (* "pattern"; *)
      "sealed"; "trait";  "tailcall";
      "volatile" ]

let stringKeywords = List.map (fun (_, w, _) -> w) keywords

(*------------------------------------------------------------------------
!* Keywords
 *-----------------------------------------------------------------------*)

let unreserve_words = 
    List.choose (fun (mode,keyword,_) -> if mode = FSHARP then Some keyword else None) keywords

let kwd_table = 
    let tab = Util.smap_create 1000 in
    List.iter (fun (mode,keyword,token) -> Util.smap_add tab keyword token) keywords;
    tab
let kwd s = Util.smap_try_find kwd_table s 
exception ReservedKeyword of string * range
exception IndentationProblem of string * range

type lexargs =  
    { getSourceDirectory: (unit -> string); }

let mkLexargs (srcdir,filename) =
  { getSourceDirectory=srcdir }

let kwd_or_id args (r:Range.range) s =
  match kwd s with 
    | Some v -> 
      if v = RESERVED then
        begin
          Util.print_string (Util.format2 "The keyword '%s' is reserved for future use by F#. (%s)" s (string_of_range r));
          (* This will give a proper syntax error at the right location for true F# files. *)
          IDENT (intern_string(s))
        end
      else v
    | None -> 
      match s with 
        | "__SOURCE_DIRECTORY__" -> 
          STRING (Bytes.string_as_unicode_bytes (args.getSourceDirectory()))
        | "__SOURCE_FILE__" -> 
          STRING (Bytes.string_as_unicode_bytes (Range.file_of_range r))
        | "__LINE__" -> 
          STRING (Bytes.string_as_unicode_bytes (string_of_int (Range.line_of_pos (Range.start_of_range r))))
        | _ -> 
          IDENT (intern_string(s))


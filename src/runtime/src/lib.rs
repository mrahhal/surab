//! The runtime for Surab.
//!
//! This lib includes infrastructure called from Surab.
//! All properly exported symbols will have "surabrt_" as a prefix.

use std::io::Write;

#[no_mangle]
pub extern fn surabrt_print_i32(v: i32) {
    println!("{}", v);
}

#[no_mangle]
pub extern fn surabrt_print_f64(v: f64) {
    println!("{}", v);
}

#[no_mangle]
pub extern fn surabrt_print_bool(b: bool) {
    println!("{}", b);
}

// For print! to flush.
#[allow(dead_code)]
fn flush_io() {
    std::io::stdout().flush().unwrap();
}

// ---
// Temporary for testing purposes
// ---

#[repr(C)]
#[derive(Debug)]
pub struct S2 {
    pub a: f64,
}

#[no_mangle]
pub extern fn print_from_rust(s2: S2) {
    println!("==========");
    println!("From rust: ");
    println!("{:?}", s2);
}

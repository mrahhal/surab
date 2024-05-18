//! The runtime for Surab.
//!
//! This lib includes infrastructure called from Surab.
//! All properly exported symbols will have "surabrt_" as a prefix.

use std::{
    ffi::{c_char, CStr},
    io::Write,
};

#[no_mangle]
pub extern "C" fn surabrt_print_i32(v: i32) {
    println!("{}", v);
}

#[no_mangle]
pub extern "C" fn surabrt_print_f64(v: f64) {
    println!("{}", v);
}

#[no_mangle]
pub extern "C" fn surabrt_print_bool(b: bool) {
    println!("{}", b);
}

#[no_mangle]
pub extern "C" fn surabrt_print_string(v: *const c_char) {
    let str = unsafe { CStr::from_ptr(v) }.to_str().unwrap();
    println!("{}", str);
}

#[no_mangle]
pub extern "C" fn surabrt_print_ptr(ptr: *const ()) {
    println!("{:?}", ptr);
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

#[repr(C)]
#[derive(Debug)]
pub struct S3 {
    pub a: f64,
    pub b: f64,
}

#[no_mangle]
pub extern "C" fn print_from_rust(s2: S2) {
    println!("==========");
    println!("From rust: ");
    println!("{:?}", s2);
    println!("==========");
}

#[no_mangle]
pub extern "C" fn get_s3_from_rust() -> S3 {
    S3 { a: 76.0, b: 73.0 }
}

#[no_mangle]
pub extern "C" fn print_s3_from_rust(s3: S3) {
    println!("==========");
    println!("From rust (S3): ");
    println!("{:?}", s3);
    println!("==========");
}

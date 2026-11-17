use rand::prelude::*;
use std::cmp::Ordering;
use std::io;

pub fn main() {
    println!("Guess a number!");

    let secret_number = rand::rng().random_range(1..=100);

    println!("Secret number: {secret_number}");

    loop {
        println!("Please input your guess: ");
        let mut guess = String::new();
        io::stdin()
            .read_line(&mut guess)
            .expect("Failed to read line...");

        let guess: u32 = match guess.trim().parse() {
            Ok(num) => num,
            Err(_) => continue,
        };

        match guess.cmp(&secret_number) {
            Ordering::Less => println!("Too small!"),
            Ordering::Greater => println!("Too big!"),
            Ordering::Equal => {
                println!("You win!");
                break;
            }
        }
    }
}
